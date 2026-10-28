using System.Security.Claims;
using System.Text.Encodings.Web;
using CareerManager.Data;
using CareerManager.Models;
using CareerManager.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CareerManager.Tests;

public sealed class OpportunityEndpointTests
{
    [Fact]
    public async Task AnonymousDirectRequestIsDenied()
    {
        var response = await GetOpportunityIndexStatusAsync(ownerId: null);
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.Unauthorized, response.Body);
    }

    [Fact]
    public async Task AuthenticatedRequestWithoutRegisteredOwnerIsDenied()
    {
        var response = await GetOpportunityIndexStatusAsync("unregistered-owner");
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.Forbidden, response.Body);
    }

    [Fact]
    public async Task DirectRequestForAnotherOwnersOpportunityReturnsNotFound()
    {
        const string connectionString =
            "Server=tcp:testing.database.windows.net,1433;Initial Catalog=CareerManager-Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=False";
        const string connectionStringEnvironmentVariable = "ConnectionStrings__CareerManager";
        var previousConnectionString = Environment.GetEnvironmentVariable(connectionStringEnvironmentVariable);
        Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, connectionString);

        try
        {
            await using var factory = new OpportunityWebApplicationFactory();
            using (var scope = factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<CareerManagerContext>();
                context.CareerOwners.AddRange(
                    new CareerOwner { Id = "owner-a" },
                    new CareerOwner { Id = "owner-b" });
                var opportunity = new CareerOpportunity
                {
                    OwnerId = "owner-b",
                    Company = "Private Company",
                    Title = "Private role"
                };
                context.CareerOpportunities.Add(opportunity);
                context.CareerDocuments.Add(new CareerDocument
                {
                    OwnerId = "owner-b",
                    Title = "Saved document",
                    Text = "Preserved saved text.",
                    Opportunity = opportunity
                });
                await context.SaveChangesAsync();
            }

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            client.DefaultRequestHeaders.Add("X-Test-Owner", "owner-a");
            var response = await client.GetAsync("/Opportunities/Details/1");

            Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);

            client.DefaultRequestHeaders.Remove("X-Test-Owner");
            client.DefaultRequestHeaders.Add("X-Test-Owner", "owner-b");
            var deleteResponse = await client.GetAsync("/Opportunities/Delete/1");
            var deletePage = await deleteResponse.Content.ReadAsStringAsync();

            Assert.Equal(System.Net.HttpStatusCode.OK, deleteResponse.StatusCode);
            Assert.Contains("Keep documents and unlink them", deletePage, StringComparison.Ordinal);
            Assert.Contains("Delete the documents", deletePage, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, previousConnectionString);
        }
    }

    private static async Task<(System.Net.HttpStatusCode StatusCode, string Body)> GetOpportunityIndexStatusAsync(
        string? ownerId)
    {
        const string connectionString =
            "Server=tcp:testing.database.windows.net,1433;Initial Catalog=CareerManager-Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=False";
        const string connectionStringEnvironmentVariable = "ConnectionStrings__CareerManager";
        var previousConnectionString = Environment.GetEnvironmentVariable(connectionStringEnvironmentVariable);
        Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, connectionString);

        try
        {
            await using var factory = new OpportunityWebApplicationFactory();
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            if (ownerId is not null)
            {
                client.DefaultRequestHeaders.Add("X-Test-Owner", ownerId);
            }

            var response = await client.GetAsync("/Opportunities");
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        finally
        {
            Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, previousConnectionString);
        }
    }

    private sealed class OpportunityWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:CareerManager"] =
                        "Server=tcp:testing.database.windows.net,1433;Initial Catalog=CareerManager-Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=False"
                }));
            builder.ConfigureServices(services =>
            {
                var databaseName = Guid.NewGuid().ToString();
                services.RemoveAll<CareerManagerContext>();
                services.RemoveAll<DbContextOptions<CareerManagerContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<CareerManagerContext>>();
                services.AddDbContext<CareerManagerContext>(options =>
                    options.UseInMemoryDatabase(databaseName));

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
            });
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestOwner";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Owner", out var ownerId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [new Claim(CareerOwnerClaim.Type, ownerId.ToString())],
                SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
