namespace CareerManager.Models;

public sealed class CareerEntry
{
    public int Id { get; set; }
    public required string OwnerId { get; set; }
    public int ProfileId { get; set; }
    public string? SyntheticKey { get; set; }
    public required string Category { get; set; }
    public required string Title { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public required string Context { get; set; }
    public string? Contribution { get; set; }
    public string? Outcome { get; set; }
    public string? Metric { get; set; }
    public string? Tags { get; set; }
    public string? SourceNote { get; set; }
    public string? SourceUrl { get; set; }
    public CareerProfile Profile { get; set; } = null!;
}
