using System.Diagnostics;
using CareerManager.ViewModels;
using Microsoft.AspNetCore.Mvc;
using CareerManager.Models;

namespace CareerManager.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Workspace(string? search, string? category, int[]? selectedIds)
    {
        return View(EvidenceWorkspacePrototype.Create(search, category, selectedIds));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
