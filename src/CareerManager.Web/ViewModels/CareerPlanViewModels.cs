using System.ComponentModel.DataAnnotations;
using CareerManager.Models;

namespace CareerManager.ViewModels;

public sealed class CareerPlanInputModel
{
    public Guid ConcurrencyToken { get; set; }

    [Required, StringLength(4000)]
    public string TargetRoles { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Strengths { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Interests { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? EvidenceGaps { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Recommendations { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? NextActions { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Progress { get; set; } = string.Empty;

    public int[] SelectedEvidenceIds { get; set; } = [];
}

public sealed class CareerPlanIndexViewModel
{
    public CareerPlan? Plan { get; init; }
    public bool IsApproved { get; init; }
}

public sealed class CareerPlanEditViewModel
{
    public int? Id { get; init; }
    public required CareerPlanInputModel Input { get; init; }
    public required IReadOnlyList<CareerEntry> AvailableEvidence { get; init; }
    public required IReadOnlySet<int> SelectedEvidenceIds { get; init; }
    public bool IsApproved { get; init; }
}

public sealed class CareerPlanGenerationReviewViewModel
{
    public required CareerPlan Plan { get; init; }
    public required IReadOnlyList<CareerEntry> SelectedEvidence { get; init; }
    public required string PreviewToken { get; init; }
    public required string ProviderName { get; init; }
    public required string ModelName { get; init; }
    public required string Region { get; init; }
    public bool SendsDataExternally { get; init; }
    public bool Consent { get; init; }
}

public sealed class CareerPlanGeneratedReviewViewModel
{
    public required CareerPlan Plan { get; init; }
    public required IReadOnlyList<CareerEntry> SelectedEvidence { get; init; }
    public required string EvidenceGaps { get; init; }
    public required string Recommendations { get; init; }
    public required string NextActions { get; init; }
    public required string ReplaceToken { get; init; }
}

public sealed class CareerPlanApprovalInputModel
{
    public Guid ConcurrencyToken { get; set; }
    public bool ContentReviewed { get; set; }
}

public sealed class CareerPlanGenerationInputModel
{
    public string PreviewToken { get; set; } = string.Empty;
    public bool Consent { get; set; }
}

public sealed class CareerPlanGeneratedProposalInputModel
{
    public string ReplaceToken { get; set; } = string.Empty;
}

public sealed class CareerPlanApprovalViewModel
{
    public required CareerPlan Plan { get; init; }
    public required IReadOnlyList<CareerEntry> SelectedEvidence { get; init; }
    public required CareerPlanApprovalInputModel Input { get; init; }
    public bool IsApproved { get; init; }
}
