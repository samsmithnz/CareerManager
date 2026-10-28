using System.ComponentModel.DataAnnotations;
using CareerManager.Models;

namespace CareerManager.ViewModels;

public sealed class ApplicationGenerationInputModel
{
    public int[] SelectedEvidenceIds { get; set; } = [];
    public string PreviewToken { get; set; } = string.Empty;
    public bool Consent { get; set; }
}

public sealed class ApplicationComparisonViewModel
{
    public required CareerOpportunity Opportunity { get; init; }
    public required IReadOnlyList<CareerEntry> AvailableEvidence { get; init; }
    public required IReadOnlySet<int> SelectedEvidenceIds { get; init; }
    public required string ProviderName { get; init; }
    public required string ModelName { get; init; }
    public required string Region { get; init; }
    public bool SendsDataExternally { get; init; }
}

public sealed class ApplicationGenerationPreviewViewModel
{
    public required CareerOpportunity Opportunity { get; init; }
    public required IReadOnlyList<CareerEntry> SelectedEvidence { get; init; }
    public required string PreviewToken { get; init; }
    public required string ProviderName { get; init; }
    public required string ModelName { get; init; }
    public required string Region { get; init; }
    public bool SendsDataExternally { get; init; }
    public bool Consent { get; init; }
}

public sealed class ApplicationDraftIndexViewModel
{
    public required IReadOnlyList<ApplicationDraftRowViewModel> Drafts { get; init; }
}

public sealed record ApplicationDraftRowViewModel(
    CareerDocument Document,
    IReadOnlyList<string> EvidenceTitles,
    int MissingEvidenceCount);

public sealed class ApplicationDraftEditInputModel
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(12000)]
    public string Text { get; set; } = string.Empty;

    public Guid ConcurrencyToken { get; set; }
}

public sealed class ApplicationDraftEditViewModel
{
    public required int Id { get; init; }
    public int? OpportunityId { get; init; }
    public required CareerDocumentType Type { get; init; }
    public required ApplicationDraftEditInputModel Input { get; init; }
    public required IReadOnlyList<EvidenceReferenceViewModel> EvidenceReferences { get; init; }
}

public sealed record EvidenceReferenceViewModel(int Id, string? Title, bool Missing);
