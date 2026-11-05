using System.Security.Claims;
using CareerManager.Data;
using CareerManager.Models;
using CareerManager.Security;
using CareerManager.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareerManager.Controllers;

[Authorize]
public sealed class CareerEvidenceController(CareerManagerContext context) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        return ownerId is null
            ? Forbid()
            : View("Edit", CreateViewModel(null, new CareerEvidenceInputModel()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CareerEvidenceInputModel input, CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View("Edit", CreateViewModel(null, input));
        }

        var profile = await context.CareerProfiles
            .Where(item => item.OwnerId == ownerId)
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            profile = new CareerProfile { OwnerId = ownerId, Name = "Career profile" };
            context.CareerProfiles.Add(profile);
            await context.SaveChangesAsync(cancellationToken);
        }

        context.CareerEntries.Add(CreateEntry(ownerId, profile.Id, input));
        await context.SaveChangesAsync(cancellationToken);
        return RedirectToAction("Workspace", "Home");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var entry = await FindOwnedEntry(id, ownerId, cancellationToken);
        return entry is null
            ? NotFound()
            : View(CreateViewModel(id, CreateInput(entry), await HasFactualReviewAsync(id, ownerId, cancellationToken)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        CareerEvidenceInputModel input,
        CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var entry = await FindOwnedEntry(id, ownerId, cancellationToken);
        if (entry is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(CreateViewModel(id, input, await HasFactualReviewAsync(id, ownerId, cancellationToken)));
        }

        ApplyInput(entry, input);
        await MarkReferencingContentForReviewAsync(id, ownerId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return RedirectToAction("Workspace", "Home");
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var entry = await FindOwnedEntry(id, ownerId, cancellationToken);
        return entry is null
            ? NotFound()
            : View(new CareerEvidenceDeleteViewModel(entry.Id, entry.Category, entry.Title));
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var entry = await FindOwnedEntry(id, ownerId, cancellationToken);
        if (entry is null)
        {
            return NotFound();
        }

        await MarkReferencingContentForReviewAsync(id, ownerId, cancellationToken);
        context.CareerEntries.Remove(entry);
        await context.SaveChangesAsync(cancellationToken);
        return RedirectToAction("Workspace", "Home");
    }

    private static CareerEntry CreateEntry(string ownerId, int profileId, CareerEvidenceInputModel input) =>
        new()
        {
            OwnerId = ownerId,
            ProfileId = profileId,
            Category = input.Category,
            Title = input.Title,
            Context = input.Context,
            StartDate = input.StartDate,
            EndDate = input.EndDate,
            Contribution = NullIfEmpty(input.Contribution),
            Outcome = NullIfEmpty(input.Outcome),
            Metric = NullIfEmpty(input.Metric),
            Tags = NormalizeTags(input.Tags),
            SourceNote = NullIfEmpty(input.SourceNote),
            SourceUrl = NullIfEmpty(input.SourceUrl)
        };

    private static void ApplyInput(CareerEntry entry, CareerEvidenceInputModel input)
    {
        entry.Category = input.Category;
        entry.Title = input.Title;
        entry.Context = input.Context;
        entry.StartDate = input.StartDate;
        entry.EndDate = input.EndDate;
        entry.Contribution = NullIfEmpty(input.Contribution);
        entry.Outcome = NullIfEmpty(input.Outcome);
        entry.Metric = NullIfEmpty(input.Metric);
        entry.Tags = NormalizeTags(input.Tags);
        entry.SourceNote = NullIfEmpty(input.SourceNote);
        entry.SourceUrl = NullIfEmpty(input.SourceUrl);
    }

    private static string? NormalizeTags(string? tags) =>
        string.IsNullOrWhiteSpace(tags)
            ? null
            : string.Join(", ", tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static CareerEvidenceInputModel CreateInput(CareerEntry entry) =>
        new()
        {
            Category = entry.Category,
            Title = entry.Title,
            Context = entry.Context,
            StartDate = entry.StartDate,
            EndDate = entry.EndDate,
            Contribution = entry.Contribution,
            Outcome = entry.Outcome,
            Metric = entry.Metric,
            Tags = entry.Tags,
            SourceNote = entry.SourceNote,
            SourceUrl = entry.SourceUrl
        };

    private static CareerEvidenceEditViewModel CreateViewModel(
        int? id,
        CareerEvidenceInputModel input,
        bool requiresFactualReview = false) =>
        new()
        {
            Id = id,
            Input = input,
            Categories = EvidenceWorkspacePrototype.CategoryOptions,
            RequiresFactualReview = requiresFactualReview
        };

    private Task<CareerEntry?> FindOwnedEntry(int id, string ownerId, CancellationToken cancellationToken) =>
        context.CareerEntries.SingleOrDefaultAsync(
            entry => entry.Id == id && entry.OwnerId == ownerId,
            cancellationToken);

    private async Task<bool> HasFactualReviewAsync(int entryId, string ownerId, CancellationToken cancellationToken)
    {
        var documentIds = await context.CareerDocumentEvidence
            .Where(reference => reference.OwnerId == ownerId && reference.CareerEntryId == entryId)
            .Select(reference => reference.CareerDocumentId)
            .ToArrayAsync(cancellationToken);
        var planIds = await context.CareerPlanEvidence
            .Where(reference => reference.OwnerId == ownerId && reference.CareerEntryId == entryId)
            .Select(reference => reference.CareerPlanId)
            .ToArrayAsync(cancellationToken);
        return await context.CareerDocuments.AnyAsync(
                document => document.OwnerId == ownerId && documentIds.Contains(document.Id)
                    && document.RequiresFactualReview, cancellationToken)
            || await context.CareerPlans.AnyAsync(
                plan => plan.OwnerId == ownerId && planIds.Contains(plan.Id)
                    && plan.RequiresFactualReview, cancellationToken);
    }

    private async Task MarkReferencingContentForReviewAsync(
        int entryId,
        string ownerId,
        CancellationToken cancellationToken)
    {
        var documentIds = await context.CareerDocumentEvidence
            .Where(reference => reference.OwnerId == ownerId && reference.CareerEntryId == entryId)
            .Select(reference => reference.CareerDocumentId)
            .ToArrayAsync(cancellationToken);
        var documents = await context.CareerDocuments
            .Where(document => document.OwnerId == ownerId && documentIds.Contains(document.Id))
            .ToListAsync(cancellationToken);
        foreach (var document in documents)
        {
            document.ApprovedAtUtc = null;
            document.ApprovedContentHash = null;
            document.RequiresFactualReview = true;
            document.UpdatedAtUtc = DateTime.UtcNow;
            document.ConcurrencyToken = Guid.NewGuid();
        }

        var planIds = await context.CareerPlanEvidence
            .Where(reference => reference.OwnerId == ownerId && reference.CareerEntryId == entryId)
            .Select(reference => reference.CareerPlanId)
            .ToArrayAsync(cancellationToken);
        var plans = await context.CareerPlans
            .Where(plan => plan.OwnerId == ownerId && planIds.Contains(plan.Id))
            .ToListAsync(cancellationToken);
        foreach (var plan in plans)
        {
            plan.ApprovedAtUtc = null;
            plan.ApprovedContentHash = null;
            plan.RequiresFactualReview = true;
            plan.UpdatedAtUtc = DateTime.UtcNow;
            plan.ConcurrencyToken = Guid.NewGuid();
        }
    }

    private async Task<string?> GetRegisteredOwnerIdAsync(CancellationToken cancellationToken)
    {
        var ownerId = User.FindFirstValue(CareerOwnerClaim.Type) ?? string.Empty;
        return ownerId.Length is > 0 and <= 256
            && await context.CareerOwners.AnyAsync(owner => owner.Id == ownerId, cancellationToken)
                ? ownerId
                : null;
    }
}
