namespace CareerManager.Models;

public sealed class CareerPlan
{
    public int Id { get; set; }
    public required string OwnerId { get; set; }
    public string TargetRoles { get; set; } = string.Empty;
    public string Strengths { get; set; } = string.Empty;
    public string Interests { get; set; } = string.Empty;
    public string EvidenceGaps { get; set; } = string.Empty;
    public string Recommendations { get; set; } = string.Empty;
    public string NextActions { get; set; } = string.Empty;
    public string Progress { get; set; } = string.Empty;
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovedContentHash { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public CareerOwner Owner { get; set; } = null!;
    public ICollection<CareerPlanEvidence> EvidenceReferences { get; } = [];
}
