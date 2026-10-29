namespace CareerManager.Models;

public sealed class DataDeletionAuditEvent
{
    public Guid RequestId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public required string Outcome { get; set; }
}
