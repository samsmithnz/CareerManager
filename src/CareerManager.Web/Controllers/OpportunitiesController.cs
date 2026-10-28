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
public sealed class OpportunitiesController(CareerManagerContext context) : Controller
{
    public async Task<IActionResult> Index(
        string? search,
        OpportunityStatus? status,
        CancellationToken cancellationToken)
    {
        if (!TryGetOwnerId(out var ownerId) || !await IsRegisteredOwner(ownerId, cancellationToken))
        {
            return Forbid();
        }

        var normalizedSearch = search?.Trim() ?? string.Empty;
        var query = context.CareerOpportunities
            .AsNoTracking()
            .Where(opportunity => opportunity.OwnerId == ownerId);

        if (status.HasValue)
        {
            if (!Enum.IsDefined(status.Value))
            {
                ModelState.AddModelError(nameof(status), "Choose a valid application status.");
                status = null;
            }
            else
            {
                query = query.Where(opportunity => opportunity.Status == status.Value);
            }
        }

        if (normalizedSearch.Length > 0)
        {
            query = query.Where(opportunity =>
                opportunity.Company.Contains(normalizedSearch)
                || opportunity.Title.Contains(normalizedSearch));
        }

        var opportunities = await query
            .OrderByDescending(opportunity => opportunity.NextActionDate)
            .ThenBy(opportunity => opportunity.Company)
            .ThenBy(opportunity => opportunity.Title)
            .ToListAsync(cancellationToken);

        return View(new OpportunityIndexViewModel
        {
            Opportunities = opportunities,
            Search = normalizedSearch,
            Status = status
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        if (!TryGetOwnerId(out var ownerId) || !await IsRegisteredOwner(ownerId, cancellationToken))
        {
            return Forbid();
        }

        var opportunity = await FindOwnedOpportunity(id, ownerId, cancellationToken);
        return opportunity is null ? NotFound() : View(opportunity);
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (!TryGetOwnerId(out var ownerId) || !await IsRegisteredOwner(ownerId, cancellationToken))
        {
            return Forbid();
        }

        return View(new OpportunityInputModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        OpportunityInputModel input,
        CancellationToken cancellationToken)
    {
        if (!TryGetOwnerId(out var ownerId) || !await IsRegisteredOwner(ownerId, cancellationToken))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        context.CareerOpportunities.Add(input.ToEntity(ownerId));
        await context.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        if (!TryGetOwnerId(out var ownerId) || !await IsRegisteredOwner(ownerId, cancellationToken))
        {
            return Forbid();
        }

        var opportunity = await FindOwnedOpportunity(id, ownerId, cancellationToken);
        return opportunity is null ? NotFound() : View(OpportunityInputModel.FromEntity(opportunity));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        OpportunityInputModel input,
        CancellationToken cancellationToken)
    {
        if (!TryGetOwnerId(out var ownerId) || !await IsRegisteredOwner(ownerId, cancellationToken))
        {
            return Forbid();
        }

        var opportunity = await FindOwnedOpportunity(id, ownerId, cancellationToken);
        if (opportunity is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        input.ApplyTo(opportunity);
        await context.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (!TryGetOwnerId(out var ownerId) || !await IsRegisteredOwner(ownerId, cancellationToken))
        {
            return Forbid();
        }

        var opportunity = await FindOwnedOpportunity(id, ownerId, cancellationToken);
        if (opportunity is null)
        {
            return NotFound();
        }

        return View(new OpportunityDeleteViewModel
        {
            Opportunity = opportunity,
            LinkedDocumentCount = await CountLinkedDocuments(id, ownerId, cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        int id,
        string? documentDisposition,
        CancellationToken cancellationToken)
    {
        if (!TryGetOwnerId(out var ownerId) || !await IsRegisteredOwner(ownerId, cancellationToken))
        {
            return Forbid();
        }

        var opportunity = await FindOwnedOpportunity(id, ownerId, cancellationToken);
        if (opportunity is null)
        {
            return NotFound();
        }

        var documents = await context.CareerDocuments
            .Where(document => document.OwnerId == ownerId && document.OpportunityId == id)
            .ToListAsync(cancellationToken);
        if (documents.Count > 0 && documentDisposition is not ("unlink" or "delete"))
        {
            ModelState.AddModelError(string.Empty, "Choose whether to keep or delete the linked documents.");
            return View(new OpportunityDeleteViewModel
            {
                Opportunity = opportunity,
                LinkedDocumentCount = documents.Count,
                DocumentDisposition = documentDisposition
            });
        }

        if (documentDisposition == "delete")
        {
            context.CareerDocuments.RemoveRange(documents);
        }
        else
        {
            foreach (var document in documents)
            {
                document.OpportunityId = null;
            }
        }

        context.CareerOpportunities.Remove(opportunity);
        await context.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private bool TryGetOwnerId(out string ownerId)
    {
        ownerId = User.FindFirstValue(CareerOwnerClaim.Type) ?? string.Empty;
        return ownerId.Length is > 0 and <= 256;
    }

    private Task<bool> IsRegisteredOwner(string ownerId, CancellationToken cancellationToken) =>
        context.CareerOwners.AnyAsync(owner => owner.Id == ownerId, cancellationToken);

    private Task<CareerOpportunity?> FindOwnedOpportunity(
        int id,
        string ownerId,
        CancellationToken cancellationToken) =>
        context.CareerOpportunities.SingleOrDefaultAsync(
            opportunity => opportunity.Id == id && opportunity.OwnerId == ownerId,
            cancellationToken);

    private Task<int> CountLinkedDocuments(
        int opportunityId,
        string ownerId,
        CancellationToken cancellationToken) =>
        context.CareerDocuments.CountAsync(
            document => document.OpportunityId == opportunityId && document.OwnerId == ownerId,
            cancellationToken);
}
