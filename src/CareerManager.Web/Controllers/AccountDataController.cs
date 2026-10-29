using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using CareerManager.Data;
using CareerManager.Security;
using CareerManager.Services;
using CareerManager.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareerManager.Controllers;

[Authorize]
public sealed class AccountDataController(
    CareerManagerContext context,
    OwnerDataService ownerDataService,
    ILogger<AccountDataController> logger) : Controller
{
    private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (await GetRegisteredOwnerIdAsync(cancellationToken) is null)
        {
            return Forbid();
        }

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        var data = await ownerDataService.ExportAsync(ownerId, cancellationToken);
        if (data is null)
        {
            return Forbid();
        }

        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, ExportJsonOptions);
        return File(bytes, "application/json; charset=utf-8", $"careermanager-data-{DateTime.UtcNow:yyyy-MM-dd}.json");
    }

    [HttpGet]
    public async Task<IActionResult> DeleteAll(CancellationToken cancellationToken)
    {
        if (await GetRegisteredOwnerIdAsync(cancellationToken) is null)
        {
            return Forbid();
        }

        return View(new DeleteAllDataInputModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAll(
        DeleteAllDataInputModel input,
        CancellationToken cancellationToken)
    {
        var ownerId = await GetRegisteredOwnerIdAsync(cancellationToken);
        if (ownerId is null)
        {
            return Forbid();
        }

        if (!input.ConfirmDeletion
            || !string.Equals(input.ConfirmationText, "DELETE ALL MY DATA", StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty,
                "Check the confirmation box and enter DELETE ALL MY DATA exactly to continue.");
            return View(input);
        }

        var requestId = Guid.NewGuid();
        try
        {
            await ownerDataService.DeleteAllAsync(ownerId, requestId, cancellationToken);
            return View("Deleted", new DataDeletionCompletedViewModel(requestId, DateTime.UtcNow));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499);
        }
        catch (Exception)
        {
            input.ErrorMessage = "Deletion could not be completed. Your content may remain; retry the deletion or contact support with the request ID shown.";
            input.RequestId = requestId;
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            logger.LogWarning("Owner data deletion failed for request {RequestId}.", requestId);
            return View(input);
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
