using CareerManager.Data;
using Microsoft.Extensions.Configuration;

namespace CareerManager.Tests;

public class DatabaseConfigurationTests
{
    [Fact]
    public void MissingConnectionStringFailsClearly()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => DatabaseConfiguration.GetRequiredConnectionString(configuration));

        Assert.Contains("ConnectionStrings:CareerManager", exception.Message);
    }

    [Fact]
    public void ConnectionStringsMustUseAnAzureSqlEndpoint()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CareerManager"] = "Server=localhost;Initial Catalog=CareerManager-Dev"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => DatabaseConfiguration.GetRequiredConnectionString(configuration));

        Assert.Contains("Azure SQL endpoint", exception.Message);
        Assert.DoesNotContain("localhost", exception.Message);
    }

    [Fact]
    public void ConnectionStringsMustValidateTheEncryptedSqlServerCertificate()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CareerManager"] =
                    "Server=tcp:sqlserver.database.windows.net;Initial Catalog=CareerManager-Dev;Encrypt=False;TrustServerCertificate=True"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => DatabaseConfiguration.GetRequiredConnectionString(configuration));

        Assert.Contains("enable encryption and validate the server certificate", exception.Message);
    }

    [Theory]
    [InlineData("CareerManager-Dev")]
    [InlineData("CareerManager-Development")]
    public void DevelopmentSeedAllowsOnlyDevelopmentDatabaseNames(string databaseName)
    {
        DatabaseConfiguration.RequireDevelopmentDatabase(ConnectionString(databaseName));
    }

    [Theory]
    [InlineData("CareerManager-Test")]
    [InlineData("CareerManager-Testing")]
    public void IntegrationTestsAllowOnlyTestDatabaseNames(string databaseName)
    {
        DatabaseConfiguration.RequireTestDatabase(ConnectionString(databaseName));
    }

    [Theory]
    [InlineData("CareerManager")]
    [InlineData("CareerManager-Production")]
    [InlineData("CareerManager-Latest")]
    [InlineData("CareerManager-Production-Test")]
    [InlineData("CareerManager-Development-Production")]
    public void SyntheticOperationsRejectNonDevelopmentAndNonTestDatabases(string databaseName)
    {
        var connectionString = ConnectionString(databaseName);

        Assert.Throws<InvalidOperationException>(
            () => DatabaseConfiguration.RequireDevelopmentDatabase(connectionString));
        Assert.Throws<InvalidOperationException>(
            () => DatabaseConfiguration.RequireTestDatabase(connectionString));
    }

    private static string ConnectionString(string databaseName) =>
        $"Server=tcp:sqlserver.database.windows.net,1433;Initial Catalog={databaseName};Integrated Security=True;Encrypt=True;TrustServerCertificate=False";
}
