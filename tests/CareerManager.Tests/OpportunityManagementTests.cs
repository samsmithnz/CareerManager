using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CareerManager.Controllers;
using CareerManager.Data;
using CareerManager.Models;
using CareerManager.Security;
using CareerManager.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareerManager.Tests;

public sealed class OpportunityManagementTests
{
    [Fact]
    public void InputValidationRequiresSubmissionDateAndRejectsUnsafeUrls()
    {
        var input = new OpportunityInputModel
        {
            Company = "Example",
            Title = "Engineer",
            Status = OpportunityStatus.Interviewing,
            JobUrl = "https://jobs.example.test/role"
        };
        var errors = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(
            input,
            new ValidationContext(input),
            errors,
            validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(input.SubmissionDate)));

        input.SubmissionDate = new DateOnly(2026, 10, 28);
        input.JobUrl = "javascript:alert(1)";
        errors.Clear();

        Assert.False(Validator.TryValidateObject(
            input,
            new ValidationContext(input),
            errors,
            validateAllProperties: true));
        Assert.Contains(errors, error => error.ErrorMessage?.Contains("HTTP or HTTPS", StringComparison.Ordinal) == true);

        input.JobUrl = "https://jobs.example.test/role";
        input.SourceUrls = "https://company.example.test/careers\nhttp://company.example.test/values";
        errors.Clear();

        Assert.True(Validator.TryValidateObject(
            input,
            new ValidationContext(input),
            errors,
            validateAllProperties: true));
    }

    [Theory]
    [InlineData(OpportunityStatus.Saved)]
    [InlineData(OpportunityStatus.Preparing)]
    [InlineData(OpportunityStatus.Applied)]
    [InlineData(OpportunityStatus.Interviewing)]
    [InlineData(OpportunityStatus.Offer)]
    [InlineData(OpportunityStatus.Rejected)]
    [InlineData(OpportunityStatus.Withdrawn)]
    public void EveryApplicationStatusIsSupported(OpportunityStatus status)
    {
        var input = new OpportunityInputModel
        {
            Company = "Example",
            Title = "Engineer",
            Status = status,
            SubmissionDate = status is OpportunityStatus.Applied
                or OpportunityStatus.Interviewing
                or OpportunityStatus.Offer
                or OpportunityStatus.Rejected
                ? new DateOnly(2026, 10, 28)
                : null
        };
        var errors = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(
            input,
            new ValidationContext(input),
            errors,
            validateAllProperties: true));
    }

    [Fact]
    public async Task ListingAndDirectLookupAreOwnerScoped()
    {
        await using var context = CreateContext();
        context.CareerOwners.AddRange(new CareerOwner { Id = "owner-a" }, new CareerOwner { Id = "owner-b" });
        var owned = Opportunity("owner-a", "Example Systems", "Engineer", OpportunityStatus.Saved);
        var other = Opportunity("owner-b", "Other Systems", "Designer", OpportunityStatus.Saved);
        context.CareerOpportunities.AddRange(owned, other);
        await context.SaveChangesAsync();

        var controller = CreateController(context, "owner-a");
        var result = Assert.IsType<ViewResult>(await controller.Index("Example", OpportunityStatus.Saved, default));
        var model = Assert.IsType<OpportunityIndexViewModel>(result.Model);

        Assert.Single(model.Opportunities);
        Assert.Equal(owned.Id, model.Opportunities[0].Id);
        Assert.IsType<NotFoundResult>(await controller.Details(other.Id, default));
    }

    [Fact]
    public async Task EditingOpportunityDoesNotChangeLinkedDocumentText()
    {
        await using var context = CreateContext();
        var owner = new CareerOwner { Id = "owner-a" };
        var opportunity = Opportunity(owner.Id, "Example Systems", "Engineer", OpportunityStatus.Saved);
        var document = new CareerDocument
        {
            OwnerId = owner.Id,
            Title = "Saved resume",
            Text = "The exact saved document text.",
            Opportunity = opportunity
        };
        context.AddRange(owner, opportunity, document);
        await context.SaveChangesAsync();

        var input = OpportunityInputModel.FromEntity(opportunity);
        input.Company = "Updated Systems";
        input.Status = OpportunityStatus.Preparing;
        input.SubmissionDate = new DateOnly(2026, 10, 28);
        input.NextActionNotes = "Prepare interview questions";
        input.NextActionDate = new DateOnly(2026, 10, 30);
        var controller = CreateController(context, owner.Id);
        var result = await controller.Edit(opportunity.Id, input, default);

        Assert.IsType<RedirectToActionResult>(result);
        var editedOpportunity = await context.CareerOpportunities.SingleAsync();
        Assert.Equal("Updated Systems", editedOpportunity.Company);
        Assert.Equal(new DateOnly(2026, 10, 28), editedOpportunity.SubmissionDate);
        Assert.Equal("Prepare interview questions", editedOpportunity.NextActionNotes);
        Assert.Equal(new DateOnly(2026, 10, 30), editedOpportunity.NextActionDate);
        Assert.Equal("The exact saved document text.", (await context.CareerDocuments.SingleAsync()).Text);
    }

    [Fact]
    public async Task DeletingOpportunityCanUnlinkOrDeleteDocumentsByExplicitChoice()
    {
        await using var context = CreateContext();
        var owner = new CareerOwner { Id = "owner-a" };
        var keptOpportunity = Opportunity(owner.Id, "Example Systems", "Engineer", OpportunityStatus.Saved);
        var deletedOpportunity = Opportunity(owner.Id, "Other Systems", "Designer", OpportunityStatus.Saved);
        var keptDocument = new CareerDocument
        {
            OwnerId = owner.Id,
            Title = "Keep this document",
            Text = "Saved content to preserve.",
            Opportunity = keptOpportunity
        };
        var deletedDocument = new CareerDocument
        {
            OwnerId = owner.Id,
            Title = "Delete this document",
            Text = "Saved content to delete.",
            Opportunity = deletedOpportunity
        };
        context.AddRange(owner, keptOpportunity, deletedOpportunity, keptDocument, deletedDocument);
        await context.SaveChangesAsync();

        var controller = CreateController(context, owner.Id);
        Assert.IsType<ViewResult>(await controller.Delete(keptOpportunity.Id, null, default));
        Assert.Contains(
            controller.ModelState.Values.SelectMany(value => value.Errors),
            error => error.ErrorMessage.Contains("Choose whether", StringComparison.Ordinal));
        Assert.IsType<RedirectToActionResult>(
            await controller.Delete(keptOpportunity.Id, "unlink", default));
        Assert.IsType<RedirectToActionResult>(
            await controller.Delete(deletedOpportunity.Id, "delete", default));

        var remainingDocument = await context.CareerDocuments.SingleAsync();
        Assert.Equal(keptDocument.Id, remainingDocument.Id);
        Assert.Null(remainingDocument.OpportunityId);
        Assert.Equal("Saved content to preserve.", remainingDocument.Text);
        Assert.Empty(await context.CareerOpportunities.ToListAsync());
    }

    private static CareerManagerContext CreateContext() =>
        new(new DbContextOptionsBuilder<CareerManagerContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CareerOpportunity Opportunity(
        string ownerId,
        string company,
        string title,
        OpportunityStatus status) =>
        new()
        {
            OwnerId = ownerId,
            Company = company,
            Title = title,
            Status = status
        };

    private static OpportunitiesController CreateController(CareerManagerContext context, string ownerId)
    {
        var controller = new OpportunitiesController(context);
        var identity = new ClaimsIdentity(
            [new Claim(CareerOwnerClaim.Type, ownerId)],
            authenticationType: "test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
        return controller;
    }
}
