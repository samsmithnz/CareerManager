using System.Security.Claims;
using CareerManager.Data;
using CareerManager.Models;
using CareerManager.Security;
using CareerManager.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CareerManager.Tests;

public sealed class OwnerDataServiceTests
{
    [Fact]
    public async Task DeleteIsIdempotentAndRetainsOwnerAndOnlyMinimalAuditFields()
    {
        await using var context = CreateContext();
        await SeedOwnerDataAsync(context);
        var service = new OwnerDataService(context);

        await service.DeleteAllAsync("owner-a", Guid.NewGuid(), default);
        await service.DeleteAllAsync("owner-a", Guid.NewGuid(), default);

        Assert.True(await context.CareerOwners.AnyAsync(owner => owner.Id == "owner-a"));
        Assert.False(await context.CareerProfiles.AnyAsync(profile => profile.OwnerId == "owner-a"));
        Assert.False(await context.CareerEntries.AnyAsync(entry => entry.OwnerId == "owner-a"));
        Assert.False(await context.CareerOpportunities.AnyAsync(opportunity => opportunity.OwnerId == "owner-a"));
        Assert.False(await context.CareerDocuments.AnyAsync(document => document.OwnerId == "owner-a"));
        Assert.False(await context.CareerDocumentEvidence.AnyAsync(reference => reference.OwnerId == "owner-a"));
        Assert.False(await context.CareerPlans.AnyAsync(plan => plan.OwnerId == "owner-a"));
        Assert.False(await context.CareerPlanEvidence.AnyAsync(reference => reference.OwnerId == "owner-a"));
        Assert.Equal(2, await context.DataDeletionAuditEvents.CountAsync());
        Assert.All(await context.DataDeletionAuditEvents.ToListAsync(),
            auditEvent => Assert.Equal("Completed", auditEvent.Outcome));
        Assert.Equal(
            new[] { nameof(DataDeletionAuditEvent.RequestId), nameof(DataDeletionAuditEvent.OccurredAtUtc), nameof(DataDeletionAuditEvent.Outcome) },
            typeof(DataDeletionAuditEvent).GetProperties().Select(property => property.Name).ToArray());
    }

    [Fact]
    public async Task CancellationLeavesContentAndRecordsOnlyCancelledOutcomeThenRetrySucceeds()
    {
        await using var context = CreateContext();
        await SeedOwnerDataAsync(context);
        var service = new OwnerDataService(context);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.DeleteAllAsync("owner-a", Guid.NewGuid(), cancellation.Token));

        Assert.True(await context.CareerProfiles.AnyAsync(profile => profile.OwnerId == "owner-a"));
        Assert.Equal("Cancelled", (await context.DataDeletionAuditEvents.SingleAsync()).Outcome);

        await service.DeleteAllAsync("owner-a", Guid.NewGuid(), default);
        Assert.False(await context.CareerProfiles.AnyAsync(profile => profile.OwnerId == "owner-a"));
    }

    [Fact]
    public async Task FailedSaveRollsBackOwnerContentAndCanBeRetried()
    {
        var interceptor = new FailNextSaveInterceptor();
        await using var context = CreateContext(interceptor);
        await SeedOwnerDataAsync(context);
        var service = new OwnerDataService(context);
        interceptor.FailNextSave = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAllAsync("owner-a", Guid.NewGuid(), default));

        Assert.True(await context.CareerProfiles.AnyAsync(profile => profile.OwnerId == "owner-a"));
        Assert.Equal("Failed", (await context.DataDeletionAuditEvents.SingleAsync()).Outcome);

        await service.DeleteAllAsync("owner-a", Guid.NewGuid(), default);
        Assert.False(await context.CareerProfiles.AnyAsync(profile => profile.OwnerId == "owner-a"));
    }

    [Fact]
    public async Task OwnerRequestsDoNotOverlapWhileGenerationOrExportIsInFlight()
    {
        await using var context = CreateContext();
        var middleware = new OwnerRequestSerializationMiddleware(context, new OwnerRequestLock());
        var firstRequestEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRequestEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRequest = middleware.InvokeAsync(CreateOwnerRequestContext(), _ =>
        {
            firstRequestEntered.SetResult();
            return releaseFirstRequest.Task;
        });
        await firstRequestEntered.Task;

        var secondRequest = middleware.InvokeAsync(CreateOwnerRequestContext(), _ =>
        {
            secondRequestEntered.SetResult();
            return Task.CompletedTask;
        });
        var secondStarted = await Task.WhenAny(secondRequestEntered.Task, Task.Delay(100));
        Assert.NotSame(secondRequestEntered.Task, secondStarted);

        releaseFirstRequest.SetResult();
        await Task.WhenAll(firstRequest, secondRequest);
        Assert.True(secondRequestEntered.Task.IsCompleted);
    }

    private static DefaultHttpContext CreateOwnerRequestContext()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(CareerOwnerClaim.Type, "owner-a")],
            "Test"));
        return context;
    }

    private static CareerManagerContext CreateContext(SaveChangesInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<CareerManagerContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor is null ? [] : [interceptor])
            .Options;
        return new CareerManagerContext(options);
    }

    private static async Task SeedOwnerDataAsync(CareerManagerContext context)
    {
        context.CareerOwners.Add(new CareerOwner { Id = "owner-a" });
        await context.SaveChangesAsync();
        var profile = new CareerProfile { OwnerId = "owner-a", Name = "Private profile" };
        context.CareerProfiles.Add(profile);
        await context.SaveChangesAsync();
        var entry = new CareerEntry
        {
            OwnerId = "owner-a",
            ProfileId = profile.Id,
            Category = "Achievement",
            Title = "Private evidence",
            Description = "Private evidence description."
        };
        var opportunity = new CareerOpportunity
        {
            OwnerId = "owner-a",
            Company = "Private company",
            Title = "Private opportunity"
        };
        context.AddRange(entry, opportunity);
        await context.SaveChangesAsync();
        var document = new CareerDocument
        {
            OwnerId = "owner-a",
            OpportunityId = opportunity.Id,
            Title = "Private draft",
            Text = "Private saved draft."
        };
        var plan = new CareerPlan
        {
            OwnerId = "owner-a",
            TargetRoles = "Private plan"
        };
        context.AddRange(document, plan);
        await context.SaveChangesAsync();
        context.AddRange(
            new CareerDocumentEvidence
            {
                OwnerId = "owner-a",
                CareerDocumentId = document.Id,
                CareerEntryId = entry.Id
            },
            new CareerPlanEvidence
            {
                OwnerId = "owner-a",
                CareerPlanId = plan.Id,
                CareerEntryId = entry.Id
            });
        await context.SaveChangesAsync();
    }

    private sealed class FailNextSaveInterceptor : SaveChangesInterceptor
    {
        public bool FailNextSave { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                throw new InvalidOperationException("Synthetic storage failure.");
            }

            return ValueTask.FromResult(result);
        }
    }
}
