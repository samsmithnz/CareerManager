namespace CareerManager.Models;

public enum OpportunityStatus
{
    Saved,
    Preparing,
    Applied,
    Interviewing,
    Offer,
    Rejected,
    Withdrawn
}

public sealed class CareerOpportunity
{
    public int Id { get; set; }
    public required string OwnerId { get; set; }
    public required string Company { get; set; }
    public required string Title { get; set; }
    public string? JobUrl { get; set; }
    public string? JobDescription { get; set; }
    public string? CompanyContext { get; set; }
    public string? SourceUrls { get; set; }
    public string? Notes { get; set; }
    public DateOnly? DateFound { get; set; }
    public DateOnly? ApplicationDeadline { get; set; }
    public DateOnly? SubmissionDate { get; set; }
    public OpportunityStatus Status { get; set; } = OpportunityStatus.Saved;
    public string? NextActionNotes { get; set; }
    public DateOnly? NextActionDate { get; set; }
    public CareerOwner Owner { get; set; } = null!;
    public ICollection<CareerDocument> Documents { get; } = [];
}
