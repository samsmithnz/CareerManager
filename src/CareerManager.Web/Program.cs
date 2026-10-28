using CareerManager.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
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
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


await app.RunAsync();

public partial class Program { }
