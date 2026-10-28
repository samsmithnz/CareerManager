using System.Threading.RateLimiting;
using CareerManager.Data;
using CareerManager.Security;
using CareerManager.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

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
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "CareerManager.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = false;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

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
app.UseRateLimiter();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


await app.RunAsync();

public partial class Program { }
