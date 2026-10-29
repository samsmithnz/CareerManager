namespace CareerManager.Models;

public sealed class CareerPlanEvidence
{
    public required string OwnerId { get; set; }
    public int CareerPlanId { get; set; }
    public int CareerEntryId { get; set; }
    public CareerPlan Plan { get; set; } = null!;
    public CareerEntry Entry { get; set; } = null!;
}
