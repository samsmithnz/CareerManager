using CareerManager.Models;
using Microsoft.EntityFrameworkCore;

namespace CareerManager.Data;

public static class SyntheticDataSeeder
{
    public const string DevelopmentOwnerId = "synthetic-development-owner";

    private static readonly (string Key, string Category, string Title, string Description)[] Entries =
    [
        ("experience", "Experience", "Synthetic project delivery", "Coordinated a fictional project and documented its outcomes."),
        ("interest", "Interest", "Synthetic problem solving", "Enjoys exploring fictional technical challenges.")
    ];

    public static async Task SeedAsync(
        CareerManagerContext context,
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var owner = await context.CareerOwners.FindAsync([ownerId], cancellationToken);
        if (owner is null)
        {
            owner = new CareerOwner { Id = ownerId };
            context.CareerOwners.Add(owner);
        }

        var profile = await context.CareerProfiles.SingleOrDefaultAsync(
            candidate => candidate.OwnerId == ownerId,
            cancellationToken);
        if (profile is null)
        {
            profile = new CareerProfile
            {
                OwnerId = ownerId,
                Name = "Synthetic development profile"
            };
            context.CareerProfiles.Add(profile);
            await context.SaveChangesAsync(cancellationToken);
        }

        foreach (var entry in Entries)
        {
            var exists = await context.CareerEntries.AnyAsync(
                candidate => candidate.OwnerId == ownerId
                    && candidate.ProfileId == profile.Id
                    && candidate.SyntheticKey == entry.Key,
                cancellationToken);
            if (!exists)
            {
                context.CareerEntries.Add(new CareerEntry
                {
                    OwnerId = ownerId,
                    ProfileId = profile.Id,
                    SyntheticKey = entry.Key,
                    Category = entry.Category,
                    Title = entry.Title,
                    Description = entry.Description
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
