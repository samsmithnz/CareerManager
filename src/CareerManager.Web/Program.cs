using System.Security.Claims;
using System.Threading.RateLimiting;
using CareerManager.Data;
using CareerManager.Security;
using CareerManager.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

var seedSyntheticData = args.Contains("--seed-synthetic", StringComparer.Ordinal);
if (seedSyntheticData && args.Any(argument => argument != "--seed-synthetic"))
{
    throw new InvalidOperationException("--seed-synthetic cannot be combined with other application arguments.");
}

if (seedSyntheticData)
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("Synthetic data seeding is allowed only in the Development environment.");
    }

    if (!string.Equals(
            builder.Configuration["CAREERMANAGER_ENABLE_SYNTHETIC_SEED"],
            "true",
            StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Set CAREERMANAGER_ENABLE_SYNTHETIC_SEED=true to explicitly enable synthetic data seeding.");
    }
}

var connectionString = DatabaseConfiguration.GetRequiredConnectionString(builder.Configuration);
builder.Services.AddDbContext<CareerManagerContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddControllersWithViews();
var ownerMapping = new MicrosoftAccountOwnerMapping(
    builder.Configuration["Authentication:Microsoft:OwnerSubject"],
    builder.Configuration["Authentication:Microsoft:OwnerId"]);
builder.Services.AddSingleton(ownerMapping);
builder.Services.AddScoped<OwnerDataService>();
builder.Services.AddSingleton<OwnerRequestLock>();
builder.Services.AddTransient<OwnerRequestSerializationMiddleware>();
var generationProvider = builder.Configuration["ApplicationGeneration:Provider"] ?? "Local";
if (string.Equals(generationProvider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase))
{
    _ = AzureOpenAiApplicationGenerationProvider.AzureOpenAiOptions.FromConfiguration(builder.Configuration);
    builder.Services.AddHttpClient<AzureOpenAiApplicationGenerationProvider>()
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
    builder.Services.AddScoped<IApplicationGenerationProvider>(services =>
        services.GetRequiredService<AzureOpenAiApplicationGenerationProvider>());
}
else if (string.Equals(generationProvider, "Local", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IApplicationGenerationProvider, DeterministicApplicationGenerationProvider>();
}
else
{
    throw new InvalidOperationException("ApplicationGeneration:Provider must be Local or AzureOpenAI.");
}
builder.Services.AddScoped<ApplicationGenerationService>();
builder.Services.AddSingleton<ApplicationGenerationQuota>();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("application-generation", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(CareerOwnerClaim.Type)?.Value
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsync(
            "Generation limit reached. Wait one minute before trying again.", cancellationToken);
    };
});
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "CareerManager.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
        options.Events.OnValidatePrincipal = async context =>
        {
            var mapping = context.HttpContext.RequestServices
                .GetRequiredService<MicrosoftAccountOwnerMapping>();
            var subject = context.Principal?.FindFirst("sub")?.Value;
            var ownerId = context.Principal?.FindFirst(CareerOwnerClaim.Type)?.Value;
            if (!mapping.TryMapValidatedSubject(subject, out var mappedOwnerId)
                || !string.Equals(ownerId, mappedOwnerId, StringComparison.Ordinal))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
        options.Events.OnRedirectToLogin = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("Sign in to access this resource.");
        };
        options.Events.OnRedirectToAccessDenied = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("This Microsoft account is not authorized to access this resource.");
        };
    })
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.Authority = "https://login.microsoftonline.com/consumers/v2.0";
        options.ClientId = builder.Configuration["Authentication:Microsoft:ClientId"] ?? string.Empty;
        options.ClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"] ?? string.Empty;
        options.CallbackPath = "/signin-microsoft";
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.RequireHttpsMetadata = true;
        options.MapInboundClaims = false;
        options.SaveTokens = false;
        options.GetClaimsFromUserInfoEndpoint = false;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Events.OnTokenValidated = context =>
        {
            var mapping = context.HttpContext.RequestServices
                .GetRequiredService<MicrosoftAccountOwnerMapping>();
            if (context.Principal?.Identity is not ClaimsIdentity identity
                || !mapping.TryMapValidatedSubject(
                    context.Principal.FindFirst("sub")?.Value,
                    out var mappedOwnerId))
            {
                context.Fail("The Microsoft account is not configured as the owner.");
                return Task.CompletedTask;
            }

            foreach (var claimIdentity in context.Principal.Identities)
            {
                foreach (var claim in claimIdentity.FindAll(CareerOwnerClaim.Type).ToArray())
                {
                    claimIdentity.RemoveClaim(claim);
                }
            }

            identity.AddClaim(new Claim(CareerOwnerClaim.Type, mappedOwnerId));
            return Task.CompletedTask;
        };
        options.Events.OnRemoteFailure = context =>
        {
            context.Response.Redirect("/Account/AuthenticationError");
            context.HandleResponse();
            return Task.CompletedTask;
        };
    });
var ownerPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .RequireClaim(CareerOwnerClaim.Type)
    .Build();
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = ownerPolicy;
    options.FallbackPolicy = ownerPolicy;
});
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "CareerManager.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

var app = builder.Build();

if (seedSyntheticData)
{
    DatabaseConfiguration.RequireDevelopmentDatabase(connectionString);
    await using var scope = app.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<CareerManagerContext>();
    await context.Database.MigrateAsync();
    await SyntheticDataSeeder.SeedAsync(context, SyntheticDataSeeder.DevelopmentOwnerId);
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<OwnerRequestSerializationMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


await app.RunAsync();

public partial class Program { }
