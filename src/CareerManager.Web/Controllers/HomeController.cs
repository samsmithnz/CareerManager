using System.Diagnostics;
using System.Security.Claims;
using CareerManager.Data;
using CareerManager.Security;
using CareerManager.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CareerManager.Models;

namespace CareerManager.Controllers;

public class HomeController(CareerManagerContext context) : Controller
{
    [AllowAnonymous]
    public IActionResult Index()
    {
        return View();
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [Authorize]
    public async Task<IActionResult> Workspace(
        string? search,
        string? category,
        int[]? selectedIds,
        CancellationToken cancellationToken)
    {
        var ownerId = User.FindFirstValue(CareerOwnerClaim.Type) ?? string.Empty;
        if (ownerId.Length is 0 or > 256
            || !await context.CareerOwners.AnyAsync(owner => owner.Id == ownerId, cancellationToken))
        {
            return Forbid();
        }

        var entries = await context.CareerEntries
            .AsNoTracking()
            .Where(entry => entry.OwnerId == ownerId)
            .ToListAsync(cancellationToken);
        var plan = await context.CareerPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OwnerId == ownerId, cancellationToken);
        var opportunities = await context.CareerOpportunities
            .AsNoTracking()
            .Where(opportunity => opportunity.OwnerId == ownerId)
            .OrderBy(opportunity => opportunity.Company)
            .ThenBy(opportunity => opportunity.Title)
            .ToListAsync(cancellationToken);
        var normalizedSearch = search?.Trim() ?? string.Empty;
        if (normalizedSearch.Length > 200)
        {
            normalizedSearch = normalizedSearch[..200];
        }

        return View(EvidenceWorkspacePrototype.CreatePersisted(
            entries,
            plan,
            opportunities,
            normalizedSearch,
            category?.Trim(),
            selectedIds?.Distinct().Take(50)));
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
