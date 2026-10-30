using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using CareerManager.Data;
using CareerManager.Models;
using CareerManager.Security;
using CareerManager.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace CareerManager.Tests;

[CollectionDefinition("Connection string environment", DisableParallelization = true)]
public sealed class ConnectionStringEnvironmentCollection
{
}

[Collection("Connection string environment")]
public sealed class AuthenticationTests
{
    [Fact]
    public async Task PrivateWorkspaceRequiresOwnerClaimButLandingAndPrivacyArePublic()
    {
        using var factory = new AuthenticationWebApplicationFactory();
        using var client = CreateClient(factory);

        var workspace = await client.GetAsync("/Home/Workspace");
        var home = await client.GetAsync("/");
        var privacy = await client.GetAsync("/Home/Privacy");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, workspace.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, home.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, privacy.StatusCode);

        client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        var authenticatedWithoutOwner = await client.GetAsync("/Home/Workspace");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, authenticatedWithoutOwner.StatusCode);
    }

    [Fact]
    public void OwnerMappingRequiresTheExactConfiguredStableSubject()
    {
        var mapping = new MicrosoftAccountOwnerMapping("stable-subject-123", "configured-owner");

        Assert.True(mapping.TryMapValidatedSubject("stable-subject-123", out var ownerId));
        Assert.Equal("configured-owner", ownerId);
        Assert.False(mapping.TryMapValidatedSubject("mutable-email@example.test", out _));
        Assert.False(mapping.TryMapValidatedSubject("another-subject", out _));
        Assert.False(new MicrosoftAccountOwnerMapping(null, "configured-owner").IsConfigured);
        Assert.False(new MicrosoftAccountOwnerMapping("stable-subject-123", null).IsConfigured);
    }

    [Fact]
    public async Task TokenValidationAddsOwnerClaimOnlyForConfiguredSubject()
    {
        using var factory = new AuthenticationWebApplicationFactory();
        var services = factory.Services;
        var scheme = await services.GetRequiredService<IAuthenticationSchemeProvider>()
            .GetSchemeAsync(OpenIdConnectDefaults.AuthenticationScheme);
        var options = services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        var cookieOptions = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        Assert.Equal("https://login.microsoftonline.com/consumers/v2.0", options.Authority);
        Assert.Equal("/signin-microsoft", options.CallbackPath);
        Assert.True(options.UsePkce);
        Assert.False(options.SaveTokens);
        Assert.Equal(CookieSecurePolicy.Always, cookieOptions.Cookie.SecurePolicy);
        Assert.True(cookieOptions.Cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Lax, cookieOptions.Cookie.SameSite);
        Assert.Equal(
            CookieSecurePolicy.Always,
            services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value.Cookie.SecurePolicy);

        var approvedContext = CreateTokenValidatedContext(
            services,
            scheme!,
            options,
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "stable-owner-subject")], "oidc")));
        await options.Events.OnTokenValidated(approvedContext);
        Assert.Equal("owner-a", approvedContext.Principal!.FindFirst(CareerOwnerClaim.Type)?.Value);

        var unapprovedContext = CreateTokenValidatedContext(
            services,
            scheme!,
            options,
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "another-subject"), new Claim("email", "owner@example.test")],
                "oidc")));
        await options.Events.OnTokenValidated(unapprovedContext);
        Assert.Null(unapprovedContext.Principal!.FindFirst(CareerOwnerClaim.Type));
        Assert.NotNull(unapprovedContext.Result?.Failure);
    }

    [Fact]
    public async Task SignInChallengesMicrosoftAndMissingOwnerConfigurationFailsClosed()
    {
        using (var factory = new AuthenticationWebApplicationFactory())
        {
            using var client = CreateClient(factory);
            var response = await client.GetAsync("/Account/SignIn?returnUrl=%2FHome%2FWorkspace");

            Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("login.microsoftonline.com", response.Headers.Location!.Host);
            Assert.Contains("client_id=test-client", response.Headers.Location.Query, StringComparison.Ordinal);
        }

        using (var factory = new AuthenticationWebApplicationFactory(configureOwner: false))
        {
            using var client = CreateClient(factory);
            var response = await client.GetAsync("/Account/SignIn");

            Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
    }

    [Fact]
    public async Task StateChangingActionsRejectRequestsWithoutAntiforgeryToken()
    {
        using var factory = new AuthenticationWebApplicationFactory();
        await SeedOwnerAsync(factory);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Owner", "owner-a");

        var response = await client.PostAsync(
            "/AccountData/DeleteAll",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>(nameof(DeleteAllDataInputModel.ConfirmDeletion), "true"),
                new KeyValuePair<string, string>(nameof(DeleteAllDataInputModel.ConfirmationText), "DELETE ALL MY DATA")
            ]));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SignOutRequiresCsrfAndExpiresTheSessionCookie()
    {
        using var factory = new AuthenticationWebApplicationFactory();
        await SeedOwnerAsync(factory);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Owner", "owner-a");

        var home = await client.GetAsync("/");
        Assert.Contains(
            home.Headers.GetValues("Set-Cookie"),
            cookie => cookie.Contains("CareerManager.Antiforgery", StringComparison.Ordinal)
                && cookie.Contains("secure", StringComparison.OrdinalIgnoreCase)
                && cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        var page = await home.Content.ReadAsStringAsync();
        var token = Regex.Match(
            page,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token));

        var signOut = await client.PostAsync(
            "/Account/SignOut",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", token)
            ]));

        Assert.Equal(System.Net.HttpStatusCode.Redirect, signOut.StatusCode);
        Assert.Equal("/Account/SignedOut", signOut.Headers.Location!.OriginalString);
        Assert.Contains(
            signOut.Headers.GetValues("Set-Cookie"),
            cookie => cookie.Contains("CareerManager.Session", StringComparison.Ordinal)
                && cookie.Contains("expires=", StringComparison.OrdinalIgnoreCase));
    }

    private static HttpClient CreateClient(AuthenticationWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static TokenValidatedContext CreateTokenValidatedContext(
        IServiceProvider services,
        AuthenticationScheme scheme,
        OpenIdConnectOptions options,
        ClaimsPrincipal principal)
    {
        var httpContext = new DefaultHttpContext { RequestServices = services };
        return new TokenValidatedContext(
            httpContext,
            scheme,
            options,
            principal,
            new AuthenticationProperties());
    }

    private static async Task SeedOwnerAsync(AuthenticationWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CareerManagerContext>();
        context.CareerOwners.Add(new CareerOwner { Id = "owner-a" });
        await context.SaveChangesAsync();
    }

    private sealed class AuthenticationWebApplicationFactory : WebApplicationFactory<Program>
    {
        private const string ConnectionStringEnvironmentVariable = "ConnectionStrings__CareerManager";
        private readonly string? previousConnectionString =
            Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        private readonly bool configureOwner;

        public AuthenticationWebApplicationFactory(bool configureOwner = true)
        {
            this.configureOwner = configureOwner;
            Environment.SetEnvironmentVariable(
                ConnectionStringEnvironmentVariable,
                "Server=tcp:testing.database.windows.net,1433;Initial Catalog=CareerManager-Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=False");
        }

        public new void Dispose()
        {
            base.Dispose();
            Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, previousConnectionString);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                var databaseName = Guid.NewGuid().ToString();
                services.RemoveAll<CareerManagerContext>();
                services.RemoveAll<DbContextOptions<CareerManagerContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<CareerManagerContext>>();
                services.AddDbContext<CareerManagerContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
                services.RemoveAll<MicrosoftAccountOwnerMapping>();
                services.AddSingleton(new MicrosoftAccountOwnerMapping(
                    configureOwner ? "stable-owner-subject" : null,
                    configureOwner ? "owner-a" : null));
                services.RemoveAll<IConfiguration>();
                services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Authentication:Microsoft:ClientId"] = "test-client",
                        ["Authentication:Microsoft:ClientSecret"] = "not-a-real-secret"
                    })
                    .Build());
                services.PostConfigure<OpenIdConnectOptions>(
                    OpenIdConnectDefaults.AuthenticationScheme,
                    options =>
                    {
                        options.ClientId = "test-client";
                        options.ClientSecret = "not-a-real-secret";
                        options.ConfigurationManager =
                            new StaticConfigurationManager<OpenIdConnectConfiguration>(
                                new OpenIdConnectConfiguration
                                {
                                    Issuer = "https://login.microsoftonline.com/consumers/v2.0",
                                    AuthorizationEndpoint =
                                        "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize"
                                });
                    });

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
        public const string SchemeName = "AuthenticationTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.ContainsKey("X-Test-Authenticated"))
            {
                var principal = new ClaimsPrincipal(
                    new ClaimsIdentity([new Claim(ClaimTypes.Name, "synthetic-non-owner")], SchemeName));
                return Task.FromResult(AuthenticateResult.Success(
                    new AuthenticationTicket(principal, SchemeName)));
            }

            if (!Request.Headers.TryGetValue("X-Test-Owner", out var ownerId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [new Claim(CareerOwnerClaim.Type, ownerId.ToString())],
                SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
