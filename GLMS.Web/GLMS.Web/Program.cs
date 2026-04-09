// ─── PROGRAM.CS — APPLICATION ENTRY POINT ────────────────────────────────────
// This is where the entire application is configured and started.
// It registers all services with the Dependency Injection (DI) container,
// configures the database, and sets up the HTTP request pipeline.
//
// DEPENDENCY INJECTION: Instead of classes creating their own dependencies
// with "new", ASP.NET Core creates and injects them automatically.
// This makes the code loosely coupled and easily testable.
//
// DESIGN PATTERNS IMPLEMENTED (From Part 1 Architecture Report):
//
// 1. FACTORY PATTERN   - Factories/ContractFactory.cs
//    - IContractFactory creates Contract objects with correct initial status
//    - Standard = Draft, Premium/Enterprise = Active
//    - Used in ContractsController.Create()
//
// 2. REPOSITORY PATTERN - Repositories/IRepositories.cs + Repositories.cs
//    - IClientRepository, IContractRepository, IServiceRequestRepository
//    - Controllers NEVER access GlmsDbContext directly
//    - Enables unit testing with mock repositories
//
// 3. OBSERVER PATTERN  - Observers/ContractObservers.cs
//    - AuditLogObserver: logs every status change
//    - EmailNotificationObserver: simulates email notifications
//    - Triggered automatically in ContractService.UpdateStatusAsync()

using Microsoft.EntityFrameworkCore;
using GLMS.Web.Data;
using GLMS.Web.Factories;
using GLMS.Web.Observers;
using GLMS.Web.Repositories;
using GLMS.Web.Services;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// ── MVC ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ── Entity Framework Core — SQL Server ───────────────────────────────────────
// Connection string is read from appsettings.json (never hardcoded)
builder.Services.AddDbContext<GlmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── ASP.NET Core Identity ─────────────────────────────────────────────────────
// FIX 1: RequireConfirmedAccount = false so seeded accounts can log in immediately
// FIX 2: Added .AddRoles<IdentityRole>() so Admin/Manager/Viewer roles work
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<GlmsDbContext>();

// ── Repository Pattern Registrations ─────────────────────────────────────────
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IContractRepository, ContractRepository>();
builder.Services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();

// ── Factory Pattern Registration ──────────────────────────────────────────────
builder.Services.AddScoped<IContractFactory, ContractFactory>();

// ── Observer Pattern Registration ─────────────────────────────────────────────
builder.Services.AddScoped<AuditLogObserver>();
builder.Services.AddScoped<EmailNotificationObserver>();
builder.Services.AddScoped<IContractService>(sp =>
{
    var service = new ContractService(sp.GetRequiredService<IContractRepository>());
    service.RegisterObserver(sp.GetRequiredService<AuditLogObserver>());
    service.RegisterObserver(sp.GetRequiredService<EmailNotificationObserver>());
    return service;
});

// ── Currency Service ──────────────────────────────────────────────────────────
builder.Services.AddHttpClient<ICurrencyService, CurrencyService>();

// ── File Service ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<IFileService, FileService>();

var app = builder.Build();

// ── Auto Migration + Role Seeding ─────────────────────────────────────────────
// FIX 3: Added DatabaseSeeder so default accounts are created on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GlmsDbContext>();
    db.Database.Migrate();
    await RoleSeeder.SeedAsync(scope.ServiceProvider);
}

// ── HTTP Pipeline ─────────────────────────────────────────────────────────────
// FIX 4: UseAuthentication must come BEFORE UseAuthorization
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication(); // ← moved before UseAuthorization
app.UseAuthorization();
app.MapRazorPages();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();