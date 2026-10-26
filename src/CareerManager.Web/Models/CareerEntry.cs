namespace CareerManager.Models;

public sealed class CareerEntry
{
    public int Id { get; set; }
    public required string OwnerId { get; set; }
    public int ProfileId { get; set; }
    public string? SyntheticKey { get; set; }
    public required string Category { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public CareerProfile Profile { get; set; } = null!;
}
