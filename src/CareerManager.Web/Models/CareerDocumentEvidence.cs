namespace CareerManager.Models;

public sealed class CareerDocumentEvidence
{
    public required string OwnerId { get; set; }
    public int CareerDocumentId { get; set; }
    public int CareerEntryId { get; set; }
    public CareerDocument Document { get; set; } = null!;
}
