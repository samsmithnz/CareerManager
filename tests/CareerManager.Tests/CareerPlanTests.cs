using System.Security.Claims;
using CareerManager.Controllers;
using CareerManager.Data;
using CareerManager.Models;
using CareerManager.Security;
using CareerManager.Services;
using CareerManager.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareerManager.Tests;

public sealed class CareerPlanTests
{
    [Fact]
    public async Task PlanAndEvidenceReferencesAreOwnerScoped()
    {
        await using var context = CreateContext();
        var (owner, _, evidence) = await SeedPlan(context, "owner-b");
        context.CareerOwners.Add(new CareerOwner { Id = "owner-a" });
        await context.SaveChangesAsync();

        var controller = CreateController(context, "owner-a");
        var otherOwnersPlan = await context.CareerPlans.SingleAsync();
        var directRequest = await controller.Edit(otherOwnersPlan.Id, default);
        var forgedReference = await controller.Create(new CareerPlanInputModel
        {
            TargetRoles = "Reporting dashboards",
            SelectedEvidenceIds = [evidence.Id]
        }, default);

        Assert.IsType<NotFoundResult>(directRequest);
        Assert.IsType<BadRequestObjectResult>(forgedReference);
        Assert.Equal(1, await context.CareerPlans.CountAsync());
        Assert.Equal(owner.Id, otherOwnersPlan.OwnerId);
    }

    [Fact]
    public async Task ApprovalRequiresReviewAndAnyEditResetsApproval()
    {
        await using var context = CreateContext();
        var (owner, plan, evidence) = await SeedPlan(context);
        var controller = CreateController(context, owner.Id);

        var incomplete = await controller.Approve(plan.Id, new CareerPlanApprovalInputModel
        {
            ConcurrencyToken = plan.ConcurrencyToken
        }, default);
        Assert.IsType<ViewResult>(incomplete);
        Assert.Null(plan.ApprovedAtUtc);

        controller.ModelState.Clear();
        var approved = await controller.Approve(plan.Id, new CareerPlanApprovalInputModel
        {
            ConcurrencyToken = plan.ConcurrencyToken,
            ContentReviewed = true
        }, default);
        Assert.IsType<RedirectToActionResult>(approved);
        Assert.NotNull(plan.ApprovedAtUtc);
        Assert.NotNull(plan.ApprovedContentHash);
        Assert.NotEqual(Guid.Empty, plan.ConcurrencyToken);

        var editController = CreateController(context, owner.Id);
        var edited = await editController.Edit(plan.Id, new CareerPlanInputModel
        {
            ConcurrencyToken = plan.ConcurrencyToken,
            TargetRoles = "Engineering manager",
            Strengths = "Clear communication",
            Interests = "Mentoring",
            EvidenceGaps = "A possible gap",
            Recommendations = "A suggestion",
            NextActions = "Arrange a mentoring conversation",
            Progress = "Started",
            SelectedEvidenceIds = [evidence.Id]
        }, default);

        Assert.True(edited is RedirectToActionResult,
            edited is ViewResult view
                ? string.Join("; ", view.ViewData.ModelState.Values.SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage))
                : edited.GetType().Name);
        Assert.Equal("Engineering manager", plan.TargetRoles);
        Assert.Equal("Arrange a mentoring conversation", plan.NextActions);
        Assert.Equal("Started", plan.Progress);
        Assert.Null(plan.ApprovedAtUtc);
        Assert.Null(plan.ApprovedContentHash);
    }

    [Fact]
    public async Task GenerationKeepsSuggestionsSeparateUntilExplicitReplace()
    {
        await using var context = CreateContext();
        var (owner, plan, evidence) = await SeedPlan(context);
        var provider = new StubProvider((request, _) =>
        {
            Assert.Equal(plan.TargetRoles, request.JobDescription);
            Assert.Single(request.Evidence);
            Assert.Equal(evidence.Id, request.Evidence[0].Id);
            return Task.FromResult(new ApplicationGenerationResult(
                [new SupportedRoleMatch("Reporting dashboards", [evidence.Id])],
                [new EvidenceGap("Reporting dashboards", "Consider building a reporting portfolio.")],
                ["Ask a mentor about analyst role expectations."],
                [],
                []));
        });
        var controller = CreateController(context, owner.Id, provider);
        var approval = await controller.Approve(plan.Id, new CareerPlanApprovalInputModel
        {
            ConcurrencyToken = plan.ConcurrencyToken,
            ContentReviewed = true
        }, default);
        Assert.IsType<RedirectToActionResult>(approval);
        Assert.NotNull(plan.ApprovedAtUtc);
        var beforeGaps = plan.EvidenceGaps;
        var beforeRecommendations = plan.Recommendations;
        var beforeActions = plan.NextActions;

        var review = Assert.IsType<ViewResult>(await controller.ReviewGeneration(plan.Id, default));
        var reviewModel = Assert.IsType<CareerPlanGenerationReviewViewModel>(review.Model);
        var missingConsent = await controller.Generate(plan.Id, new CareerPlanGenerationInputModel
        {
            PreviewToken = reviewModel.PreviewToken
        }, default);
        Assert.IsType<ViewResult>(missingConsent);
        Assert.Equal(0, provider.CallCount);

        var generated = Assert.IsType<ViewResult>(await controller.Generate(plan.Id, new CareerPlanGenerationInputModel
        {
            PreviewToken = reviewModel.PreviewToken,
            Consent = true
        }, default));
        var generatedModel = Assert.IsType<CareerPlanGeneratedReviewViewModel>(generated.Model);

        Assert.Equal(1, provider.CallCount);
        Assert.Contains("Possible alignment:", generatedModel.Recommendations, StringComparison.Ordinal);
        Assert.Equal("GeneratedReview", generated.ViewName);
        Assert.Equal(beforeGaps, plan.EvidenceGaps);
        Assert.Equal(beforeRecommendations, plan.Recommendations);
        Assert.Equal(beforeActions, plan.NextActions);
        Assert.NotNull(plan.ApprovedAtUtc);
        Assert.Equal("Built reporting dashboards using SQL for teams.", evidence.Description);

        var replaced = await controller.ReplaceGenerated(plan.Id, new CareerPlanGeneratedProposalInputModel
        {
            ReplaceToken = generatedModel.ReplaceToken
        }, default);

        Assert.IsType<RedirectToActionResult>(replaced);
        Assert.Contains("Consider building a reporting portfolio.", plan.EvidenceGaps, StringComparison.Ordinal);
        Assert.Contains("Possible alignment:", plan.Recommendations, StringComparison.Ordinal);
        Assert.Contains("Ask a mentor", plan.NextActions, StringComparison.Ordinal);
        Assert.Equal("Progress note", plan.Progress);
        Assert.Null(plan.ApprovedAtUtc);
        Assert.Null(plan.ApprovedContentHash);
    }

    [Fact]
    public async Task ProviderFailureLeavesSavedPlanAndApprovalUntouched()
    {
        await using var context = CreateContext();
        var (owner, plan, _) = await SeedPlan(context);
        var controller = CreateController(context, owner.Id, new StubProvider((_, _) =>
            throw new InvalidOperationException("private provider detail")));
        var approved = await controller.Approve(plan.Id, new CareerPlanApprovalInputModel
        {
            ConcurrencyToken = plan.ConcurrencyToken,
            ContentReviewed = true
        }, default);
        Assert.IsType<RedirectToActionResult>(approved);
        var token = plan.ConcurrencyToken;

        var review = Assert.IsType<ViewResult>(await controller.ReviewGeneration(plan.Id, default));
        var reviewModel = Assert.IsType<CareerPlanGenerationReviewViewModel>(review.Model);
        var result = Assert.IsType<ViewResult>(await controller.Generate(plan.Id, new CareerPlanGenerationInputModel
        {
            PreviewToken = reviewModel.PreviewToken,
            Consent = true
        }, default));

        Assert.Equal("ReviewGeneration", result.ViewName);
        Assert.Equal("Existing gap", plan.EvidenceGaps);
        Assert.Equal("Existing recommendation", plan.Recommendations);
        Assert.Equal("Existing action", plan.NextActions);
        Assert.NotNull(plan.ApprovedAtUtc);
        Assert.NotNull(plan.ApprovedContentHash);
        Assert.Equal(token, plan.ConcurrencyToken);
    }

    [Fact]
    public async Task StaleEditsAndGeneratedProposalsCannotOverwriteNewerContent()
    {
        await using var context = CreateContext();
        var (owner, plan, _) = await SeedPlan(context);
        var controller = CreateController(context, owner.Id, new DeterministicApplicationGenerationProvider());
        var staleInput = new CareerPlanInputModel
        {
            ConcurrencyToken = Guid.NewGuid(),
            TargetRoles = "Stale edit"
        };
        Assert.IsType<ConflictObjectResult>(await controller.Edit(plan.Id, staleInput, default));
        Assert.Equal("Reporting dashboards", plan.TargetRoles);

        var review = Assert.IsType<ViewResult>(await controller.ReviewGeneration(plan.Id, default));
        var reviewModel = Assert.IsType<CareerPlanGenerationReviewViewModel>(review.Model);
        var generated = Assert.IsType<ViewResult>(await controller.Generate(plan.Id, new CareerPlanGenerationInputModel
        {
            PreviewToken = reviewModel.PreviewToken,
            Consent = true
        }, default));
        var generatedModel = Assert.IsType<CareerPlanGeneratedReviewViewModel>(generated.Model);

        plan.NextActions = "A newer owner edit";
        plan.ConcurrencyToken = Guid.NewGuid();
        await context.SaveChangesAsync();
        var replace = await controller.ReplaceGenerated(plan.Id, new CareerPlanGeneratedProposalInputModel
        {
            ReplaceToken = generatedModel.ReplaceToken
        }, default);

        Assert.IsType<ConflictObjectResult>(replace);
        Assert.Equal("A newer owner edit", plan.NextActions);
        Assert.Equal("Existing gap", plan.EvidenceGaps);
    }

    [Fact]
    public async Task AnonymousOwnerClaimCannotAccessPlan()
    {
        await using var context = CreateContext();
        var (_, plan, _) = await SeedPlan(context);
        var controller = CreateController(context, null);

        Assert.IsType<ForbidResult>(await controller.Edit(plan.Id, default));
        Assert.IsType<ForbidResult>(await controller.Index(default));
    }

    private static async Task<(CareerOwner Owner, CareerPlan Plan, CareerEntry Evidence)> SeedPlan(
        CareerManagerContext context,
        string ownerId = "owner-a")
    {
        var owner = new CareerOwner { Id = ownerId };
        var profile = new CareerProfile { OwnerId = ownerId, Name = "Private profile" };
        var evidence = new CareerEntry
        {
            OwnerId = ownerId,
            Profile = profile,
            Category = "Achievement",
            Title = "Reporting dashboard",
            Description = "Built reporting dashboards using SQL for teams."
        };
        var plan = new CareerPlan
        {
            OwnerId = ownerId,
            TargetRoles = "Reporting dashboards",
            Strengths = "Communicating results",
            Interests = "Data analysis",
            EvidenceGaps = "Existing gap",
            Recommendations = "Existing recommendation",
            NextActions = "Existing action",
            Progress = "Progress note"
        };
        plan.EvidenceReferences.Add(new CareerPlanEvidence
        {
            OwnerId = ownerId,
            CareerEntryId = evidence.Id,
            Entry = evidence,
            Plan = plan
        });
        context.AddRange(owner, profile, evidence, plan);
        await context.SaveChangesAsync();
        return (owner, plan, evidence);
    }

    private static CareerManagerContext CreateContext() =>
        new(new DbContextOptionsBuilder<CareerManagerContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CareerPlansController CreateController(
        CareerManagerContext context,
        string? ownerId,
        IApplicationGenerationProvider? provider = null)
    {
        var controller = new CareerPlansController(
            context,
            new ApplicationGenerationService(provider ?? new DeterministicApplicationGenerationProvider()),
            new ApplicationGenerationQuota(),
            new EphemeralDataProtectionProvider());
        var claims = ownerId is null
            ? []
            : new[] { new Claim(CareerOwnerClaim.Type, ownerId) };
        var identity = new ClaimsIdentity(claims, ownerId is null ? null : "test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private sealed class StubProvider(
        Func<ApplicationGenerationRequest, CancellationToken, Task<ApplicationGenerationResult>> generate)
        : IApplicationGenerationProvider
    {
        public int CallCount { get; private set; }
        public string ProviderName => "Test provider";
        public string ModelName => "test-model";
        public string Region => "test-region";
        public bool SendsDataExternally => false;

        public Task<ApplicationGenerationResult> GenerateAsync(
            ApplicationGenerationRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return generate(request, cancellationToken);
        }
    }
}
