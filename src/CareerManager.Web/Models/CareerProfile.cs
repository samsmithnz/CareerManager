namespace CareerManager.Models;

public sealed class CareerProfile
{
    public int Id { get; set; }
    public required string OwnerId { get; set; }
    public required string Name { get; set; }
    public CareerOwner Owner { get; set; } = null!;
    public ICollection<CareerEntry> Entries { get; } = [];
}
