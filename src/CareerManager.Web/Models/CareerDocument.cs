namespace CareerManager.Models;

public sealed class CareerDocument
{
    public int Id { get; set; }
    public required string OwnerId { get; set; }
    public int? OpportunityId { get; set; }
    public required string Title { get; set; }
    public required string Text { get; set; }
    public CareerDocumentType Type { get; set; }
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovedContentHash { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public CareerOwner Owner { get; set; } = null!;
    public CareerOpportunity? Opportunity { get; set; }
    public ICollection<CareerDocumentEvidence> EvidenceReferences { get; } = [];
}

public enum CareerDocumentType
{
    Other,
    Comparison,
    Resume,
    CoverLetter
}
