namespace CareerManager.Models;

public sealed class CareerDocument
{
    public int Id { get; set; }
    public required string OwnerId { get; set; }
    public int? OpportunityId { get; set; }
    public required string Title { get; set; }
    public required string Text { get; set; }
    public CareerOwner Owner { get; set; } = null!;
    public CareerOpportunity? Opportunity { get; set; }
}
