using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CareerManager.Data;
using CareerManager.Models;
using CareerManager.Security;
using CareerManager.Services;
using CareerManager.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CareerManager.Controllers;

[Authorize]
public sealed class DocumentsController(
    CareerManagerContext context,
    ILogger<DocumentsController> logger) : Controller
{
    public async Task<IActionResult> Index(int? opportunityId, CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        if (opportunityId.HasValue && !await context.CareerOpportunities.AnyAsync(
                opportunity => opportunity.Id == opportunityId && opportunity.OwnerId == ownerId,
                cancellationToken))
        {
            return NotFound();
        }

        var documents = await context.CareerDocuments
            .AsNoTracking()
            .Where(document => document.OwnerId == ownerId
                && (!opportunityId.HasValue || document.OpportunityId == opportunityId))
            .OrderByDescending(document => document.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
        var documentIds = documents.Select(document => document.Id).ToArray();
        var references = await context.CareerDocumentEvidence
            .AsNoTracking()
            .Where(reference => reference.OwnerId == ownerId && documentIds.Contains(reference.CareerDocumentId))
            .ToListAsync(cancellationToken);
        var evidenceIds = references.Select(reference => reference.CareerEntryId).Distinct().ToArray();
        var existingEvidence = await context.CareerEntries
            .AsNoTracking()
            .Where(entry => entry.OwnerId == ownerId && evidenceIds.Contains(entry.Id))
            .ToDictionaryAsync(entry => entry.Id, cancellationToken);

        return View(new ApplicationDraftIndexViewModel
        {
            Drafts = documents.Select(document =>
            {
                var ids = references
                    .Where(reference => reference.CareerDocumentId == document.Id)
                    .Select(reference => reference.CareerEntryId)
                    .ToArray();
                return new ApplicationDraftRowViewModel(
                    document,
                    ids.Where(existingEvidence.ContainsKey).Select(id => existingEvidence[id].Title).ToArray(),
                    ids.Count(id => !existingEvidence.ContainsKey(id)),
                    IsApproved(document));
            }).ToArray()
        });
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var document = await FindOwnedDocument(id, ownerId, cancellationToken);
        return document is null
            ? NotFound()
            : View(await CreateEditViewModel(document, ownerId, cancellationToken));
    }

    public async Task<IActionResult> Review(int id, CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var document = await FindOwnedDocument(id, ownerId, cancellationToken);
        return document is null
            ? NotFound()
            : View(await CreateEditViewModel(document, ownerId, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        ApplicationDraftEditInputModel input,
        CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var document = await FindOwnedDocument(id, ownerId, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (document.ConcurrencyToken != input.ConcurrencyToken)
        {
            return Conflict("This draft changed after you opened it. Reload it before saving to avoid overwriting newer edits.");
        }

        if (!ModelState.IsValid)
        {
            return View(await CreateEditViewModel(document, ownerId, cancellationToken, input));
        }

        document.Title = input.Title;
        document.Text = input.Text;
        document.ApprovedAtUtc = null;
        document.ApprovedContentHash = null;
        document.UpdatedAtUtc = DateTime.UtcNow;
        document.ConcurrencyToken = Guid.NewGuid();
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("This draft changed after you opened it. Reload it before saving to avoid overwriting newer edits.");
        }

        return RedirectToAction(nameof(Index), new { opportunityId = document.OpportunityId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(
        int id,
        ApplicationDraftApprovalInputModel input,
        CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var document = await FindOwnedDocument(id, ownerId, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (!IsExportable(document.Type))
        {
            return BadRequest("Only resumes and cover letters can be approved.");
        }

        if (document.ConcurrencyToken != input.ConcurrencyToken)
        {
            return Conflict("This document changed after you reviewed it. Reload it and review the current content.");
        }

        if (!input.FactualClaimsReviewed)
        {
            ModelState.AddModelError(
                $"{nameof(ApplicationDraftEditViewModel.ApprovalInput)}.{nameof(input.FactualClaimsReviewed)}",
                "Confirm that you reviewed every factual claim against the evidence.");
        }

        if (!input.UnsupportedClaimsResolved)
        {
            ModelState.AddModelError(
                $"{nameof(ApplicationDraftEditViewModel.ApprovalInput)}.{nameof(input.UnsupportedClaimsResolved)}",
                "Resolve unsupported factual claims before approving this document.");
        }

        if (!ModelState.IsValid)
        {
            return View("Review", await CreateEditViewModel(
                document,
                ownerId,
                cancellationToken,
                approvalInput: input));
        }

        document.ApprovedAtUtc = DateTime.UtcNow;
        document.ApprovedContentHash = GetContentHash(document.Title, document.Text);
        document.UpdatedAtUtc = DateTime.UtcNow;
        document.ConcurrencyToken = Guid.NewGuid();
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("This document changed while you were approving it. Reload it and review the current content.");
        }

        return RedirectToAction(nameof(Edit), new { id = document.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Export(
        int id,
        string format,
        Guid concurrencyToken,
        CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var document = await FindOwnedDocument(id, ownerId, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (document.ConcurrencyToken != concurrencyToken)
        {
            return Conflict("This document changed after the export link was created. Reload it before downloading.");
        }

        if (!IsExportable(document.Type))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(format))
        {
            return NotFound();
        }

        var normalizedFormat = format.ToLowerInvariant();
        var (contentType, extension) = normalizedFormat switch
        {
            "pdf" => ("application/pdf", "pdf"),
            "docx" => ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx"),
            _ => (null, null)
        };
        if (contentType is null || extension is null)
        {
            return NotFound();
        }

        try
        {
            var content = normalizedFormat == "pdf"
                ? DocumentExportService.GeneratePdf(document)
                : DocumentExportService.GenerateDocx(document);
            var currentToken = await context.CareerDocuments
                .AsNoTracking()
                .Where(current => current.Id == id && current.OwnerId == ownerId)
                .Select(current => (Guid?)current.ConcurrencyToken)
                .SingleOrDefaultAsync(cancellationToken);
            if (currentToken != concurrencyToken)
            {
                return Conflict("This document changed while the export was being generated. Reload it before downloading.");
            }

            Response.Headers.CacheControl = "no-store";
            return File(content, contentType, $"{document.Type.ToString().ToLowerInvariant()}-{document.Id}.{extension}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Could not generate an export for document {DocumentId}", id);
            return Problem(
                title: "Export failed",
                detail: "The document could not be exported. Its saved content has not been changed.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private async Task<ApplicationDraftEditViewModel> CreateEditViewModel(
        CareerDocument document,
        string ownerId,
        CancellationToken cancellationToken,
        ApplicationDraftEditInputModel? input = null,
        ApplicationDraftApprovalInputModel? approvalInput = null)
    {
        var references = await context.CareerDocumentEvidence
            .AsNoTracking()
            .Where(reference => reference.OwnerId == ownerId && reference.CareerDocumentId == document.Id)
            .ToListAsync(cancellationToken);
        var ids = references.Select(reference => reference.CareerEntryId).Distinct().ToArray();
        var existingEvidence = await context.CareerEntries
            .AsNoTracking()
            .Where(entry => entry.OwnerId == ownerId && ids.Contains(entry.Id))
            .ToDictionaryAsync(entry => entry.Id, cancellationToken);

        return new ApplicationDraftEditViewModel
        {
            Id = document.Id,
            OpportunityId = document.OpportunityId,
            Type = document.Type,
            Input = input ?? new ApplicationDraftEditInputModel
            {
                Title = document.Title,
                Text = document.Text,
                ConcurrencyToken = document.ConcurrencyToken
            },
            ApprovalInput = approvalInput ?? new ApplicationDraftApprovalInputModel
            {
                ConcurrencyToken = document.ConcurrencyToken
            },
            ApprovedAtUtc = document.ApprovedAtUtc,
            IsApproved = IsApproved(document),
            CanExport = IsExportable(document.Type),
            EvidenceReferences = ids
                .Select(id => existingEvidence.TryGetValue(id, out var entry)
                    ? new EvidenceReferenceViewModel(id, entry.Title, false)
                    : new EvidenceReferenceViewModel(id, null, true))
                .ToArray()
        };
    }

    private Task<CareerDocument?> FindOwnedDocument(int id, string ownerId, CancellationToken cancellationToken) =>
        context.CareerDocuments.SingleOrDefaultAsync(
            document => document.Id == id && document.OwnerId == ownerId,
            cancellationToken);

    private static bool IsExportable(CareerDocumentType type) =>
        type is CareerDocumentType.Resume or CareerDocumentType.CoverLetter;

    private static bool IsApproved(CareerDocument document) =>
        document.ApprovedAtUtc.HasValue
        && string.Equals(document.ApprovedContentHash, GetContentHash(document.Title, document.Text), StringComparison.Ordinal);

    private static string GetContentHash(string title, string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{title}\0{text}")));

    private async Task<string?> GetRegisteredOwnerIdAsync(CancellationToken cancellationToken)
    {
        var ownerId = User.FindFirstValue(CareerOwnerClaim.Type) ?? string.Empty;
        return ownerId.Length is > 0 and <= 256
            && await context.CareerOwners.AnyAsync(owner => owner.Id == ownerId, cancellationToken)
                ? ownerId
                : null;
    }
}
