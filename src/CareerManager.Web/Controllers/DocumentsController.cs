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
public sealed class DocumentsController(CareerManagerContext context) : Controller
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
                    ids.Count(id => !existingEvidence.ContainsKey(id)));
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

    private async Task<ApplicationDraftEditViewModel> CreateEditViewModel(
        CareerDocument document,
        string ownerId,
        CancellationToken cancellationToken,
        ApplicationDraftEditInputModel? input = null)
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

    private async Task<string?> GetRegisteredOwnerIdAsync(CancellationToken cancellationToken)
    {
        var ownerId = User.FindFirstValue(CareerOwnerClaim.Type) ?? string.Empty;
        return ownerId.Length is > 0 and <= 256
            && await context.CareerOwners.AnyAsync(owner => owner.Id == ownerId, cancellationToken)
                ? ownerId
                : null;
    }
}
