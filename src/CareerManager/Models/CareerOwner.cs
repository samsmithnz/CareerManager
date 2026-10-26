namespace CareerManager.Models;

public sealed class CareerOwner
{
    public required string Id { get; set; }
    public ICollection<CareerProfile> Profiles { get; } = [];
}
