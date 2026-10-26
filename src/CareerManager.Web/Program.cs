using CareerManager.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var seedSyntheticData = args.Length == 1 && args[0] == "--seed-synthetic";
if (args.Length > 0 && !seedSyntheticData)
{
    throw new InvalidOperationException("The only supported command is --seed-synthetic.");
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

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


await app.RunAsync();
