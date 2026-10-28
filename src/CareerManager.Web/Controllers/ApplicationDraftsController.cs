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
public sealed class ApplicationDraftsController(
    CareerManagerContext context,
    ApplicationGenerationService generationService,
    ApplicationGenerationQuota generationQuota,
    IDataProtectionProvider dataProtectionProvider) : Controller
{
    private readonly ITimeLimitedDataProtector _previewProtector =
        dataProtectionProvider.CreateProtector("CareerManager.ApplicationGenerationPreview.v1")
            .ToTimeLimitedDataProtector();

    [HttpGet]
    public async Task<IActionResult> Compare(
        int id,
        int[]? selectedEvidenceIds,
        CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var opportunity = await FindOwnedOpportunity(id, ownerId, cancellationToken);
        if (opportunity is null)
        {
            return NotFound();
        }

        var selectedIds = selectedEvidenceIds?.ToHashSet() ?? [];
        return View(await CreateComparisonViewModel(opportunity, ownerId, selectedIds, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(
        int id,
        ApplicationGenerationInputModel input,
        CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var opportunity = await FindOwnedOpportunity(id, ownerId, cancellationToken);
        if (opportunity is null)
        {
            return NotFound();
        }

        var selectedIds = (input.SelectedEvidenceIds ?? []).Distinct().ToHashSet();
        var evidence = await FindSelectedEvidence(ownerId, selectedIds, cancellationToken);
        if (evidence is null)
        {
            return BadRequest("Select only evidence owned by the authenticated owner.");
        }

        var request = CreateGenerationRequest(opportunity, evidence);
        var inputLength = GenerationInputLength(request);
        if (selectedIds.Count > ApplicationGenerationService.MaximumEvidenceCount
            || inputLength > ApplicationGenerationService.MaximumInputCharacters)
        {
            ModelState.AddModelError(string.Empty,
                "The request exceeds the safe input limit. Select at most 10 evidence items and shorten the role context.");
            return View("Compare", await CreateComparisonViewModel(
                opportunity, ownerId, selectedIds, cancellationToken));
        }

        return View("Confirm", CreatePreviewViewModel(
            opportunity, evidence, ownerId, request));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("application-generation")]
    public async Task<IActionResult> Generate(
        int id,
        ApplicationGenerationInputModel input,
        CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var opportunity = await FindOwnedOpportunity(id, ownerId, cancellationToken);
        if (opportunity is null)
        {
            return NotFound();
        }

        var selectedIds = (input.SelectedEvidenceIds ?? []).Distinct().ToHashSet();
        var evidence = await FindSelectedEvidence(ownerId, selectedIds, cancellationToken);
        if (evidence is null)
        {
            return BadRequest("Select only evidence owned by the authenticated owner.");
        }

        var request = CreateGenerationRequest(opportunity, evidence);
        if (!IsValidPreviewToken(input.PreviewToken, opportunity, ownerId, request))
        {
            ModelState.AddModelError(string.Empty, "The role or selected evidence changed. Review the current data again before generating.");
            return View("Compare", await CreateComparisonViewModel(
                opportunity, ownerId, selectedIds, cancellationToken));
        }

        if (!input.Consent)
        {
            ModelState.AddModelError(nameof(input.Consent), "Confirm that you reviewed and consent to processing the data shown.");
            return View("Confirm", CreatePreviewViewModel(
                opportunity, evidence, ownerId, request, input.PreviewToken, false));
        }

        if (!generationQuota.TryConsume(ownerId, DateOnly.FromDateTime(DateTime.UtcNow)))
        {
            ModelState.AddModelError(string.Empty, "The daily generation limit has been reached. Try again after 00:00 UTC.");
            return View("Confirm", CreatePreviewViewModel(
                opportunity, evidence, ownerId, request, input.PreviewToken, input.Consent));
        }

        ApplicationGenerationResult generated;
        try
        {
            generated = await generationService.GenerateAsync(request, cancellationToken);
        }
        catch (ApplicationGenerationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("Confirm", CreatePreviewViewModel(
                opportunity, evidence, ownerId, request, input.PreviewToken, input.Consent));
        }

        var now = DateTime.UtcNow;
        var documents = new[]
        {
            CreateDocument(ownerId, opportunity.Id, CareerDocumentType.Comparison,
                "Role comparison", ApplicationGenerationService.RenderComparison(generated), now),
            CreateDocument(ownerId, opportunity.Id, CareerDocumentType.Resume,
                "Resume draft", ApplicationGenerationService.RenderResume(generated), now),
            CreateDocument(ownerId, opportunity.Id, CareerDocumentType.CoverLetter,
                "Cover letter draft",
                ApplicationGenerationService.RenderCoverLetter(generated, opportunity.Title, opportunity.Company), now)
        };
        context.CareerDocuments.AddRange(documents);
        foreach (var document in documents)
        {
            foreach (var entry in evidence)
            {
                context.CareerDocumentEvidence.Add(new CareerDocumentEvidence
                {
                    OwnerId = ownerId,
                    CareerDocumentId = document.Id,
                    CareerEntryId = entry.Id,
                    Document = document
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return RedirectToAction("Index", "Documents", new { opportunityId = opportunity.Id });
    }

    private async Task<ApplicationComparisonViewModel> CreateComparisonViewModel(
        CareerOpportunity opportunity,
        string ownerId,
        IReadOnlySet<int> selectedIds,
        CancellationToken cancellationToken)
    {
        var evidence = await context.CareerEntries
            .AsNoTracking()
            .Where(entry => entry.OwnerId == ownerId)
            .OrderBy(entry => entry.Category)
            .ThenBy(entry => entry.Title)
            .ToListAsync(cancellationToken);
        var validIds = evidence.Select(entry => entry.Id).ToHashSet();
        return new ApplicationComparisonViewModel
        {
            Opportunity = opportunity,
            AvailableEvidence = evidence,
            SelectedEvidenceIds = selectedIds.Intersect(validIds).ToHashSet(),
            ProviderName = generationService.ProviderName,
            ModelName = generationService.ModelName,
            Region = generationService.Region,
            SendsDataExternally = generationService.SendsDataExternally
        };
    }

    private ApplicationGenerationPreviewViewModel CreatePreviewViewModel(
        CareerOpportunity opportunity,
        IReadOnlyList<CareerEntry> evidence,
        string ownerId,
        ApplicationGenerationRequest request,
        string? previewToken = null,
        bool consent = false) =>
        new()
        {
            Opportunity = opportunity,
            SelectedEvidence = evidence,
            PreviewToken = previewToken ?? CreatePreviewToken(opportunity, ownerId, request),
            ProviderName = generationService.ProviderName,
            ModelName = generationService.ModelName,
            Region = generationService.Region,
            SendsDataExternally = generationService.SendsDataExternally,
            Consent = consent
        };

    private async Task<List<CareerEntry>?> FindSelectedEvidence(
        string ownerId,
        IReadOnlySet<int> selectedIds,
        CancellationToken cancellationToken)
    {
        var evidence = await context.CareerEntries
            .AsNoTracking()
            .Where(entry => entry.OwnerId == ownerId && selectedIds.Contains(entry.Id))
            .OrderBy(entry => entry.Id)
            .ToListAsync(cancellationToken);
        return evidence.Count == selectedIds.Count ? evidence : null;
    }

    private ApplicationGenerationRequest CreateGenerationRequest(
        CareerOpportunity opportunity,
        IReadOnlyList<CareerEntry> evidence) =>
        new(
            opportunity.Company,
            opportunity.Title,
            opportunity.JobDescription ?? string.Empty,
            opportunity.CompanyContext ?? string.Empty,
            evidence.Select(entry => new GenerationEvidence(entry.Id, entry.Category, entry.Title, entry.Description)).ToArray());

    private string CreatePreviewToken(
        CareerOpportunity opportunity,
        string ownerId,
        ApplicationGenerationRequest request)
    {
        var preview = JsonSerializer.Serialize(new
        {
            OwnerId = ownerId,
            OpportunityId = opportunity.Id,
            Request = request,
            Provider = generationService.ProviderName,
            Model = generationService.ModelName,
            Region = generationService.Region
        });
        return _previewProtector.Protect(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(preview))),
            TimeSpan.FromMinutes(10));
    }

    private bool IsValidPreviewToken(
        string previewToken,
        CareerOpportunity opportunity,
        string ownerId,
        ApplicationGenerationRequest request)
    {
        if (string.IsNullOrEmpty(previewToken) || previewToken.Length > 4096)
        {
            return false;
        }

        try
        {
            var receivedFingerprint = _previewProtector.Unprotect(previewToken, out _);
            var expectedPreview = JsonSerializer.Serialize(new
            {
                OwnerId = ownerId,
                OpportunityId = opportunity.Id,
                Request = request,
                Provider = generationService.ProviderName,
                Model = generationService.ModelName,
                Region = generationService.Region
            });
            var expectedFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expectedPreview)));
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

    private static int GenerationInputLength(ApplicationGenerationRequest request) =>
        request.Company.Length
        + request.RoleTitle.Length
        + request.JobDescription.Length
        + request.CompanyContext.Length
        + request.Evidence.Sum(evidence => evidence.Category.Length + evidence.Title.Length + evidence.Description.Length);

    private Task<CareerOpportunity?> FindOwnedOpportunity(
        int id,
        string ownerId,
        CancellationToken cancellationToken) =>
        context.CareerOpportunities.SingleOrDefaultAsync(
            opportunity => opportunity.Id == id && opportunity.OwnerId == ownerId,
            cancellationToken);

    private async Task<string?> GetRegisteredOwnerIdAsync(CancellationToken cancellationToken)
    {
        var ownerId = User.FindFirstValue(CareerOwnerClaim.Type) ?? string.Empty;
        return ownerId.Length is > 0 and <= 256
            && await context.CareerOwners.AnyAsync(owner => owner.Id == ownerId, cancellationToken)
                ? ownerId
                : null;
    }

    private static CareerDocument CreateDocument(
        string ownerId,
        int opportunityId,
        CareerDocumentType type,
        string title,
        string text,
        DateTime createdAtUtc) =>
        new()
        {
            OwnerId = ownerId,
            OpportunityId = opportunityId,
            Type = type,
            Title = title,
            Text = text,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc
        };
}
