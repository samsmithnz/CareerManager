using CareerManager.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerManager.IntegrationTests;

public class AzureSqlFoundationTests
{
    [Fact]
    [Trait("Category", "AzureSql")]
    public async Task MigrationAndSyntheticSeedingAreRepeatableAndCleanedUp()
    {
        var connectionString = Environment.GetEnvironmentVariable("CAREERMANAGER_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Missing CAREERMANAGER_TEST_SQL_CONNECTION. Set it to an authorized, isolated Azure SQL database named CareerManager-Test or CareerManager-Testing before running the Azure SQL integration tests.");
        }

        DatabaseConfiguration.RequireTestDatabase(connectionString);
        var options = new DbContextOptionsBuilder<CareerManagerContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using var context = new CareerManagerContext(options);
        await context.Database.MigrateAsync();

        var ownerId = $"integration-test-{Guid.NewGuid():N}";
        try
        {
            await SyntheticDataSeeder.SeedAsync(context, ownerId);
            var firstEntryCount = await context.CareerEntries.CountAsync(entry => entry.OwnerId == ownerId);
            await SyntheticDataSeeder.SeedAsync(context, ownerId);

            Assert.Equal(2, firstEntryCount);
            Assert.Equal(firstEntryCount, await context.CareerEntries.CountAsync(entry => entry.OwnerId == ownerId));
            Assert.Single(await context.CareerProfiles.Where(profile => profile.OwnerId == ownerId).ToListAsync());
        }
        finally
        {
            var owner = await context.CareerOwners.SingleOrDefaultAsync(candidate => candidate.Id == ownerId);
            if (owner is not null)
            {
                context.CareerOwners.Remove(owner);
                await context.SaveChangesAsync();
            }
        }
    }
}
