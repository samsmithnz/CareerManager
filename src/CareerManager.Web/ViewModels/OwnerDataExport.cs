namespace CareerManager.ViewModels;

public sealed class OwnerDataExport
{
    public int SchemaVersion { get; init; } = 1;
    public DateTime ExportedAtUtc { get; init; }
    public required string OwnerId { get; init; }
    public required IReadOnlyList<CareerProfileExport> Profiles { get; init; }
    public required IReadOnlyList<CareerEntryExport> Entries { get; init; }
    public required IReadOnlyList<CareerOpportunityExport> Opportunities { get; init; }
    public required IReadOnlyList<CareerDocumentExport> Documents { get; init; }
    public required IReadOnlyList<CareerDocumentEvidenceExport> DocumentEvidence { get; init; }
    public required IReadOnlyList<CareerPlanExport> Plans { get; init; }
    public required IReadOnlyList<CareerPlanEvidenceExport> PlanEvidence { get; init; }
}

public sealed record CareerProfileExport(int Id, string OwnerId, string Name);

public sealed record CareerEntryExport(
    int Id,
    string OwnerId,
    int ProfileId,
    string? SyntheticKey,
    string Category,
    string Title,
    string Description);

public sealed record CareerOpportunityExport(
    int Id,
    string OwnerId,
    string Company,
    string Title,
    string? JobUrl,
    string? JobDescription,
    string? CompanyContext,
    string? SourceUrls,
    string? Notes,
    DateOnly? DateFound,
    DateOnly? ApplicationDeadline,
    DateOnly? SubmissionDate,
    string Status,
    string? NextActionNotes,
    DateOnly? NextActionDate);

public sealed record CareerDocumentExport(
    int Id,
    string OwnerId,
    int? OpportunityId,
    string Title,
    string Text,
    string Type,
    Guid ConcurrencyToken,
    DateTime? ApprovedAtUtc,
    string? ApprovedContentHash,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CareerDocumentEvidenceExport(string OwnerId, int CareerDocumentId, int CareerEntryId);

public sealed record CareerPlanExport(
    int Id,
    string OwnerId,
    string TargetRoles,
    string Strengths,
    string Interests,
    string EvidenceGaps,
    string Recommendations,
    string NextActions,
    string Progress,
    Guid ConcurrencyToken,
    DateTime? ApprovedAtUtc,
    string? ApprovedContentHash,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CareerPlanEvidenceExport(string OwnerId, int CareerPlanId, int CareerEntryId);

public sealed class DeleteAllDataInputModel
{
    public bool ConfirmDeletion { get; set; }
    public string? ConfirmationText { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? RequestId { get; set; }
}

public sealed record DataDeletionCompletedViewModel(Guid RequestId, DateTime CompletedAtUtc);
