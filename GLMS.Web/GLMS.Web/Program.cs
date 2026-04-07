// ─── PROGRAM.CS — APPLICATION ENTRY POINT ────────────────────────────────────
// This is where the entire application is configured and started.
// It registers all services with the Dependency Injection (DI) container,
// configures the database, and sets up the HTTP request pipeline.
//
// DEPENDENCY INJECTION: Instead of classes creating their own dependencies
// with "new", ASP.NET Core creates and injects them automatically.
// This makes the code loosely coupled and easily testable.

using Microsoft.EntityFrameworkCore;
using GLMS.Web.Data;
using GLMS.Web.Factories;
using GLMS.Web.Observers;
using GLMS.Web.Repositories;
using GLMS.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ── MVC ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ── Entity Framework Core — SQL Server ───────────────────────────────────────
// Registers GlmsDbContext with the DI container
// Connection string is read from appsettings.json (never hardcoded)
builder.Services.AddDbContext<GlmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Repository Pattern Registrations ─────────────────────────────────────────
// AddScoped = one instance per HTTP request (correct for database operations)
// Controllers receive interfaces — they don't know about the concrete classes
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IContractRepository, ContractRepository>();
builder.Services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();

// ── Factory Pattern Registration ──────────────────────────────────────────────
builder.Services.AddScoped<IContractFactory, ContractFactory>();

// ── Observer Pattern Registration ─────────────────────────────────────────────
// Register the two observers
builder.Services.AddScoped<AuditLogObserver>();
builder.Services.AddScoped<EmailNotificationObserver>();

// Register ContractService with observers injected and registered at startup
// This wires up the Observer Pattern — both observers subscribe to the service
builder.Services.AddScoped<IContractService>(sp =>
{
    var service = new ContractService(sp.GetRequiredService<IContractRepository>());
    // Register both observers so they get notified on every status change
    service.RegisterObserver(sp.GetRequiredService<AuditLogObserver>());
    service.RegisterObserver(sp.GetRequiredService<EmailNotificationObserver>());
    return service;
});

// ── Currency Service ──────────────────────────────────────────────────────────
// AddHttpClient creates a managed HttpClient — avoids socket exhaustion issues
builder.Services.AddHttpClient<ICurrencyService, CurrencyService>();

// ── File Service ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<IFileService, FileService>();

var app = builder.Build();

// ── Auto Migration ────────────────────────────────────────────────────────────
// Automatically applies any pending migrations when the app starts
// This ensures the database schema is always up to date
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GlmsDbContext>();
    db.Database.Migrate();
}

// ── HTTP Pipeline ─────────────────────────────────────────────────────────────
app.UseStaticFiles(); // Serves CSS, JS, images from wwwroot/
app.UseRouting();
app.UseAuthorization();

// Default route — maps URLs to Controller/Action/Id
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();