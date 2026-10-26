using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace CareerManager.Data;

public static partial class DatabaseConfiguration
{
    public const string ConnectionStringName = "CareerManager";
    private static readonly string[] AzureSqlDnsSuffixes =
    [
        ".database.windows.net",
        ".database.chinacloudapi.cn",
        ".database.usgovcloudapi.net"
    ];

    public static string GetRequiredConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Missing required connection string 'ConnectionStrings:CareerManager'. Configure it with .NET User Secrets for local development or ConnectionStrings__CareerManager in the authorized environment.");
        }

        RequireAzureSqlEndpoint(connectionString);
        return connectionString;
    }

    public static void RequireDevelopmentDatabase(string connectionString)
    {
        RequireDatabaseName(
            connectionString,
            DevelopmentDatabaseNameRegex(),
            "Synthetic seeding",
            "CareerManager-Dev or CareerManager-Development");
    }

    public static void RequireTestDatabase(string connectionString)
    {
        RequireDatabaseName(
            connectionString,
            TestDatabaseNameRegex(),
            "Azure SQL integration tests",
            "CareerManager-Test or CareerManager-Testing");
    }

    private static void RequireDatabaseName(
        string connectionString,
        Regex allowedName,
        string operation,
        string allowedDatabaseNames)
    {
        var builder = ParseConnectionString(connectionString);
        RequireAzureSqlEndpoint(builder);

        if (!allowedName.IsMatch(builder.InitialCatalog))
        {
            throw new InvalidOperationException(
                $"{operation} must use a dedicated Azure SQL database named {allowedDatabaseNames}.");
        }
    }

    private static void RequireAzureSqlEndpoint(string connectionString) =>
        RequireAzureSqlEndpoint(ParseConnectionString(connectionString));

    private static void RequireAzureSqlEndpoint(SqlConnectionStringBuilder builder)
    {
        var dataSource = builder.DataSource;
        if (dataSource.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            dataSource = dataSource[4..];
        }

        var host = dataSource.Split(',')[0];
        if (!AzureSqlDnsSuffixes.Any(suffix =>
                host.Length > suffix.Length && host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "The configured database must use an Azure SQL endpoint; local SQL instances and database containers are not supported.");
        }

        if (!builder.Encrypt || builder.TrustServerCertificate)
        {
            throw new InvalidOperationException(
                "Azure SQL connections must enable encryption and validate the server certificate.");
        }
    }

    private static SqlConnectionStringBuilder ParseConnectionString(string connectionString)
    {
        try
        {
            return new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("The configured Azure SQL connection string is invalid.");
        }
    }

    [GeneratedRegex(@"^CareerManager[-_](?:dev|development)$", RegexOptions.IgnoreCase)]
    private static partial Regex DevelopmentDatabaseNameRegex();

    [GeneratedRegex(@"^CareerManager[-_](?:test|testing)$", RegexOptions.IgnoreCase)]
    private static partial Regex TestDatabaseNameRegex();
}
