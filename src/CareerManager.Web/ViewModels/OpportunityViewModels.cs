using CareerManager.Models;

namespace CareerManager.ViewModels;

public sealed class OpportunityIndexViewModel
{
    public required IReadOnlyList<CareerOpportunity> Opportunities { get; init; }
    public string Search { get; init; } = string.Empty;
    public OpportunityStatus? Status { get; init; }
}
