using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CareerManager.Data;
using CareerManager.Models;
using CareerManager.Security;
using CareerManager.Services;
using CareerManager.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CareerManager.Controllers;

[Authorize]
public sealed class CareerPlansController(
    CareerManagerContext context,
    ApplicationGenerationService generationService,
    ApplicationGenerationQuota generationQuota,
    IDataProtectionProvider dataProtectionProvider) : Controller
{
    private const int MaximumProtectedProposalLength = 60_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ITimeLimitedDataProtector _previewProtector =
        dataProtectionProvider.CreateProtector("CareerManager.CareerPlanGenerationPreview.v1")
            .ToTimeLimitedDataProtector();

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var plan = await context.CareerPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OwnerId == ownerId, cancellationToken);
        return View(new CareerPlanIndexViewModel
        {
            Plan = plan,
            IsApproved = plan is not null && await IsApprovedAsync(plan, ownerId, cancellationToken)
        });
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        if (await context.CareerPlans.AnyAsync(plan => plan.OwnerId == ownerId, cancellationToken))
        {
            return RedirectToAction(nameof(Index));
        }

        return View("Edit", await CreateEditViewModel(ownerId, null, null, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CareerPlanInputModel input, CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        if (await context.CareerPlans.AnyAsync(plan => plan.OwnerId == ownerId, cancellationToken))
        {
            return Conflict("A career plan already exists. Reload it before making changes.");
        }

        var selectedEvidence = await FindSelectedEvidence(ownerId, input.SelectedEvidenceIds, cancellationToken);
        if (selectedEvidence is null)
        {
            return BadRequest("Select at most 10 distinct evidence records owned by the authenticated owner.");
        }

        if (!ModelState.IsValid)
        {
            return View("Edit", await CreateEditViewModel(ownerId, null, input, cancellationToken));
        }

        var now = DateTime.UtcNow;
        var plan = new CareerPlan
        {
            OwnerId = ownerId,
            TargetRoles = input.TargetRoles,
            Strengths = input.Strengths ?? string.Empty,
            Interests = input.Interests ?? string.Empty,
            EvidenceGaps = input.EvidenceGaps ?? string.Empty,
            Recommendations = input.Recommendations ?? string.Empty,
            NextActions = input.NextActions ?? string.Empty,
            Progress = input.Progress ?? string.Empty,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        AddEvidenceReferences(plan, ownerId, selectedEvidence);
        context.CareerPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Edit), new { id = plan.Id });
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var plan = await FindOwnedPlan(id, ownerId, cancellationToken);
        return plan is null
            ? NotFound()
            : View(await CreateEditViewModel(ownerId, plan, null, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        CareerPlanInputModel input,
        CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var plan = await FindOwnedPlan(id, ownerId, cancellationToken);
        if (plan is null)
        {
            return NotFound();
        }

        if (plan.ConcurrencyToken != input.ConcurrencyToken)
        {
            return Conflict("This plan changed after you opened it. Reload it before saving to avoid overwriting newer edits.");
        }

        var selectedEvidence = await FindSelectedEvidence(ownerId, input.SelectedEvidenceIds, cancellationToken);
        if (selectedEvidence is null)
        {
            return BadRequest("Select at most 10 distinct evidence records owned by the authenticated owner.");
        }

        if (!ModelState.IsValid)
        {
            return View(await CreateEditViewModel(ownerId, plan, input, cancellationToken));
        }

        ApplyInput(plan, input);
        UpdateEvidenceReferences(plan, ownerId, selectedEvidence);
        ClearApproval(plan);
        plan.UpdatedAtUtc = DateTime.UtcNow;
        plan.ConcurrencyToken = Guid.NewGuid();
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("This plan changed after you opened it. Reload it before saving to avoid overwriting newer edits.");
        }

        return RedirectToAction(nameof(Edit), new { id = plan.Id });
    }

    public async Task<IActionResult> ReviewGeneration(int id, CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var plan = await FindOwnedPlan(id, ownerId, cancellationToken);
        if (plan is null)
        {
            return NotFound();
        }

        var evidence = await GetSelectedEvidence(plan, ownerId, cancellationToken);
        return View(CreateGenerationReviewViewModel(plan, evidence, ownerId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("application-generation")]
    public async Task<IActionResult> Generate(
        int id,
        CareerPlanGenerationInputModel input,
        CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var plan = await FindOwnedPlan(id, ownerId, cancellationToken);
        if (plan is null)
        {
            return NotFound();
        }

        var evidence = await GetSelectedEvidence(plan, ownerId, cancellationToken);
        var review = CreateGenerationReviewViewModel(plan, evidence, ownerId, input.Consent);
        if (!IsValidPreviewToken(input.PreviewToken, review.PreviewToken))
        {
            return Conflict("The plan or selected evidence changed. Review the current data again before generating.");
        }

        if (!input.Consent)
        {
            ModelState.AddModelError(nameof(input.Consent), "Confirm that you reviewed and consent to processing the data shown.");
            return View("ReviewGeneration", review);
        }

        if (!generationQuota.TryConsume(ownerId, DateOnly.FromDateTime(DateTime.UtcNow)))
        {
            ModelState.AddModelError(string.Empty, "The daily generation limit has been reached. Try again after 00:00 UTC.");
            return View("ReviewGeneration", review);
        }

        ApplicationGenerationResult generated;
        try
        {
            generated = await generationService.GenerateAsync(CreateGenerationRequest(plan, evidence), cancellationToken);
        }
        catch (ApplicationGenerationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("ReviewGeneration", review);
        }

        var gaps = string.Join(Environment.NewLine, generated.Gaps.Select(gap =>
            $"{gap.Requirement}: {gap.Question}"));
        var recommendations = string.Join(Environment.NewLine,
            generated.Matches.Select(match =>
                    $"Possible alignment: {match.Requirement} [Evidence: {string.Join(", ", match.EvidenceIds)}]")
                .Concat(generated.Questions.Select(question => $"Consider: {question}")));
        var nextActions = string.Join(Environment.NewLine,
            generated.Gaps.Select(gap => gap.Question).Concat(generated.Questions).Distinct(StringComparer.Ordinal));
        if (gaps.Length > 4000 || recommendations.Length > 4000 || nextActions.Length > 4000)
        {
            ModelState.AddModelError(string.Empty,
                "The generated plan is too long to save. Shorten your selected evidence or target roles and try again.");
            return View("ReviewGeneration", review);
        }

        var proposal = new CareerPlanGeneratedProposal(
            ownerId,
            plan.Id,
            plan.ConcurrencyToken,
            gaps,
            recommendations,
            nextActions);
        return View("GeneratedReview", new CareerPlanGeneratedReviewViewModel
        {
            Plan = plan,
            SelectedEvidence = evidence,
            EvidenceGaps = gaps,
            Recommendations = recommendations,
            NextActions = nextActions,
            ReplaceToken = _previewProtector.Protect(
                JsonSerializer.Serialize(proposal, JsonOptions),
                TimeSpan.FromMinutes(10))
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplaceGenerated(
        int id,
        CareerPlanGeneratedProposalInputModel input,
        CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var plan = await FindOwnedPlan(id, ownerId, cancellationToken);
        if (plan is null)
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(input.ReplaceToken)
            || input.ReplaceToken.Length > MaximumProtectedProposalLength)
        {
            return BadRequest("The generated review has expired. Review the current plan and generate it again.");
        }

        CareerPlanGeneratedProposal? proposal;
        try
        {
            var json = _previewProtector.Unprotect(input.ReplaceToken, out _);
            proposal = JsonSerializer.Deserialize<CareerPlanGeneratedProposal>(json, JsonOptions);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or ArgumentException)
        {
            return BadRequest("The generated review has expired. Review the current plan and generate it again.");
        }

        if (proposal is null
            || proposal.OwnerId != ownerId
            || proposal.PlanId != id
            || proposal.ConcurrencyToken != plan.ConcurrencyToken)
        {
            return Conflict("The plan changed after generation. Review the current plan and generate it again.");
        }

        plan.EvidenceGaps = proposal.EvidenceGaps;
        plan.Recommendations = proposal.Recommendations;
        plan.NextActions = proposal.NextActions;
        ClearApproval(plan);
        plan.UpdatedAtUtc = DateTime.UtcNow;
        plan.ConcurrencyToken = Guid.NewGuid();
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("The plan changed after generation. Review the current plan and generate it again.");
        }

        return RedirectToAction(nameof(Edit), new { id = plan.Id });
    }

    public async Task<IActionResult> ReviewApproval(int id, CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var plan = await FindOwnedPlan(id, ownerId, cancellationToken);
        if (plan is null)
        {
            return NotFound();
        }

        return View(await CreateApprovalViewModel(plan, ownerId, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(
        int id,
        CareerPlanApprovalInputModel input,
        CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var plan = await FindOwnedPlan(id, ownerId, cancellationToken);
        if (plan is null)
        {
            return NotFound();
        }

        if (plan.ConcurrencyToken != input.ConcurrencyToken)
        {
            return Conflict("This plan changed after you reviewed it. Reload it and review the current content.");
        }

        if (!input.ContentReviewed)
        {
            ModelState.AddModelError(nameof(input.ContentReviewed), "Confirm that you reviewed the current plan and its evidence.");
            return View("ReviewApproval", await CreateApprovalViewModel(plan, ownerId, cancellationToken, input));
        }

        plan.ApprovedAtUtc = DateTime.UtcNow;
        plan.ApprovedContentHash = await GetContentHashAsync(plan, ownerId, cancellationToken);
        plan.UpdatedAtUtc = DateTime.UtcNow;
        plan.ConcurrencyToken = Guid.NewGuid();
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("This plan changed while you were approving it. Reload it and review the current content.");
        }

        return RedirectToAction(nameof(Edit), new { id = plan.Id });
    }

    private async Task<CareerPlanEditViewModel> CreateEditViewModel(
        string ownerId,
        CareerPlan? plan,
        CareerPlanInputModel? input,
        CancellationToken cancellationToken)
    {
        var evidence = await GetAvailableEvidence(ownerId, cancellationToken);
        var selectedIds = input?.SelectedEvidenceIds.ToHashSet()
            ?? plan?.EvidenceReferences.Select(reference => reference.CareerEntryId).ToHashSet()
            ?? [];
        return new CareerPlanEditViewModel
        {
            Id = plan?.Id,
            Input = input ?? (plan is null ? new CareerPlanInputModel() : CreateInputModel(plan)),
            AvailableEvidence = evidence,
            SelectedEvidenceIds = selectedIds.Intersect(evidence.Select(entry => entry.Id)).ToHashSet(),
            IsApproved = plan is not null && await IsApprovedAsync(plan, ownerId, cancellationToken)
        };
    }

    private async Task<CareerPlanApprovalViewModel> CreateApprovalViewModel(
        CareerPlan plan,
        string ownerId,
        CancellationToken cancellationToken,
        CareerPlanApprovalInputModel? input = null)
    {
        var evidence = await GetSelectedEvidence(plan, ownerId, cancellationToken);
        return new CareerPlanApprovalViewModel
        {
            Plan = plan,
            SelectedEvidence = evidence,
            Input = input ?? new CareerPlanApprovalInputModel { ConcurrencyToken = plan.ConcurrencyToken },
            IsApproved = await IsApprovedAsync(plan, ownerId, cancellationToken)
        };
    }

    private CareerPlanGenerationReviewViewModel CreateGenerationReviewViewModel(
        CareerPlan plan,
        IReadOnlyList<CareerEntry> evidence,
        string ownerId,
        bool consent = false)
    {
        var request = CreateGenerationRequest(plan, evidence);
        return new CareerPlanGenerationReviewViewModel
        {
            Plan = plan,
            SelectedEvidence = evidence,
            PreviewToken = CreatePreviewToken(plan, ownerId, request),
            ProviderName = generationService.ProviderName,
            ModelName = generationService.ModelName,
            Region = generationService.Region,
            SendsDataExternally = generationService.SendsDataExternally,
            Consent = consent
        };
    }

    private string CreatePreviewToken(
        CareerPlan plan,
        string ownerId,
        ApplicationGenerationRequest request)
    {
        var preview = JsonSerializer.Serialize(new
        {
            OwnerId = ownerId,
            PlanId = plan.Id,
            plan.ConcurrencyToken,
            Request = request,
            Provider = generationService.ProviderName,
            Model = generationService.ModelName,
            Region = generationService.Region
        }, JsonOptions);
        return _previewProtector.Protect(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(preview))),
            TimeSpan.FromMinutes(10));
    }

    private bool IsValidPreviewToken(string token, string expectedToken)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 4096)
        {
            return false;
        }

        try
        {
            var receivedFingerprint = _previewProtector.Unprotect(token, out _);
            var expectedFingerprint = _previewProtector.Unprotect(expectedToken, out _);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(receivedFingerprint),
                Encoding.UTF8.GetBytes(expectedFingerprint));
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static ApplicationGenerationRequest CreateGenerationRequest(
        CareerPlan plan,
        IReadOnlyList<CareerEntry> evidence) =>
        new(
            "Career planning",
            plan.TargetRoles,
            plan.TargetRoles,
            $"Strengths to build on:\n{plan.Strengths}\nInterests:\n{plan.Interests}",
            evidence.Select(entry => new GenerationEvidence(entry.Id, entry.Category, entry.Title, entry.Description)).ToArray());

    private static void AddEvidenceReferences(
        CareerPlan plan,
        string ownerId,
        IReadOnlyList<CareerEntry> evidence)
    {
        foreach (var entry in evidence)
        {
            plan.EvidenceReferences.Add(new CareerPlanEvidence
            {
                OwnerId = ownerId,
                CareerEntryId = entry.Id,
                Entry = entry,
                Plan = plan
            });
        }
    }

    private void UpdateEvidenceReferences(
        CareerPlan plan,
        string ownerId,
        IReadOnlyList<CareerEntry> evidence)
    {
        var selectedIds = evidence.Select(entry => entry.Id).ToHashSet();
        foreach (var reference in plan.EvidenceReferences
                     .Where(reference => !selectedIds.Contains(reference.CareerEntryId))
                     .ToArray())
        {
            plan.EvidenceReferences.Remove(reference);
            context.CareerPlanEvidence.Remove(reference);
        }

        var existingIds = plan.EvidenceReferences.Select(reference => reference.CareerEntryId).ToHashSet();
        foreach (var entry in evidence.Where(entry => !existingIds.Contains(entry.Id)))
        {
            plan.EvidenceReferences.Add(new CareerPlanEvidence
            {
                OwnerId = ownerId,
                CareerEntryId = entry.Id,
                Entry = entry,
                Plan = plan
            });
        }
    }

    private static void ApplyInput(CareerPlan plan, CareerPlanInputModel input)
    {
        plan.TargetRoles = input.TargetRoles;
        plan.Strengths = input.Strengths ?? string.Empty;
        plan.Interests = input.Interests ?? string.Empty;
        plan.EvidenceGaps = input.EvidenceGaps ?? string.Empty;
        plan.Recommendations = input.Recommendations ?? string.Empty;
        plan.NextActions = input.NextActions ?? string.Empty;
        plan.Progress = input.Progress ?? string.Empty;
    }

    private static CareerPlanInputModel CreateInputModel(CareerPlan plan) =>
        new()
        {
            ConcurrencyToken = plan.ConcurrencyToken,
            TargetRoles = plan.TargetRoles,
            Strengths = plan.Strengths,
            Interests = plan.Interests,
            EvidenceGaps = plan.EvidenceGaps,
            Recommendations = plan.Recommendations,
            NextActions = plan.NextActions,
            Progress = plan.Progress,
            SelectedEvidenceIds = plan.EvidenceReferences.Select(reference => reference.CareerEntryId).ToArray()
        };

    private static void ClearApproval(CareerPlan plan)
    {
        plan.ApprovedAtUtc = null;
        plan.ApprovedContentHash = null;
    }

    private async Task<bool> IsApprovedAsync(
        CareerPlan plan,
        string ownerId,
        CancellationToken cancellationToken) =>
        plan.ApprovedAtUtc.HasValue
        && string.Equals(
            plan.ApprovedContentHash,
            await GetContentHashAsync(plan, ownerId, cancellationToken),
            StringComparison.Ordinal);

    private async Task<string> GetContentHashAsync(
        CareerPlan plan,
        string ownerId,
        CancellationToken cancellationToken)
    {
        var evidence = await context.CareerPlanEvidence
            .AsNoTracking()
            .Where(reference => reference.OwnerId == ownerId && reference.CareerPlanId == plan.Id)
            .OrderBy(reference => reference.CareerEntryId)
            .Select(reference => new
            {
                reference.CareerEntryId,
                reference.Entry.Category,
                reference.Entry.Title,
                reference.Entry.Description
            })
            .ToArrayAsync(cancellationToken);
        var content = JsonSerializer.Serialize(new
        {
            plan.TargetRoles,
            plan.Strengths,
            plan.Interests,
            plan.EvidenceGaps,
            plan.Recommendations,
            plan.NextActions,
            plan.Progress,
            Evidence = evidence
        }, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }

    private async Task<IReadOnlyList<CareerEntry>> GetAvailableEvidence(
        string ownerId,
        CancellationToken cancellationToken) =>
        await context.CareerEntries
            .AsNoTracking()
            .Where(entry => entry.OwnerId == ownerId)
            .OrderBy(entry => entry.Category)
            .ThenBy(entry => entry.Title)
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<CareerEntry>> GetSelectedEvidence(
        CareerPlan plan,
        string ownerId,
        CancellationToken cancellationToken)
    {
        var evidenceIds = plan.EvidenceReferences.Select(reference => reference.CareerEntryId).Distinct().ToArray();
        return await context.CareerEntries
            .AsNoTracking()
            .Where(entry => entry.OwnerId == ownerId && evidenceIds.Contains(entry.Id))
            .OrderBy(entry => entry.Category)
            .ThenBy(entry => entry.Title)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<CareerEntry>?> FindSelectedEvidence(
        string ownerId,
        int[]? selectedEvidenceIds,
        CancellationToken cancellationToken)
    {
        var requestedIds = selectedEvidenceIds ?? [];
        if (requestedIds.Length > ApplicationGenerationService.MaximumEvidenceCount
            || requestedIds.Distinct().Count() != requestedIds.Length)
        {
            return null;
        }

        var evidence = await context.CareerEntries
            .AsNoTracking()
            .Where(entry => entry.OwnerId == ownerId && requestedIds.Contains(entry.Id))
            .OrderBy(entry => entry.Id)
            .ToListAsync(cancellationToken);
        return evidence.Count == requestedIds.Length ? evidence : null;
    }

    private Task<CareerPlan?> FindOwnedPlan(int id, string ownerId, CancellationToken cancellationToken) =>
        context.CareerPlans
            .Include(plan => plan.EvidenceReferences)
            .SingleOrDefaultAsync(plan => plan.Id == id && plan.OwnerId == ownerId, cancellationToken);

    private async Task<string?> GetRegisteredOwnerIdAsync(CancellationToken cancellationToken)
    {
        var ownerId = User.FindFirstValue(CareerOwnerClaim.Type) ?? string.Empty;
        return ownerId.Length is > 0 and <= 256
            && await context.CareerOwners.AnyAsync(owner => owner.Id == ownerId, cancellationToken)
                ? ownerId
                : null;
    }

    private sealed record CareerPlanGeneratedProposal(
        string OwnerId,
        int PlanId,
        Guid ConcurrencyToken,
        string EvidenceGaps,
        string Recommendations,
        string NextActions);

}
