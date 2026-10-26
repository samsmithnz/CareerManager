using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CareerManager.Data;

public sealed class CareerManagerContextFactory : IDesignTimeDbContextFactory<CareerManagerContext>
{
    public CareerManagerContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var configurationBuilder = new ConfigurationBuilder();

        if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
        {
            configurationBuilder.AddUserSecrets(typeof(CareerManagerContext).Assembly, optional: true);
        }

        var configuration = configurationBuilder.AddEnvironmentVariables().Build();
        var connectionString = DatabaseConfiguration.GetRequiredConnectionString(configuration);
        var options = new DbContextOptionsBuilder<CareerManagerContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new CareerManagerContext(options);
    }
}
