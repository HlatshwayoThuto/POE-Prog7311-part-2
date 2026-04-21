// WHAT IS THIS FILE?
// This is the VERY FIRST file that runs when the application starts.
// Think of it like opening a restaurant for the day:
//
// STEP 1 (builder phase) = PREPARATION before opening
// - Set up the kitchen (database)
// - Hire the staff (register services)
// - Set the menu (configure routes)
// - Install the security system (authentication)
//
// STEP 2 (app phase) = OPENING the restaurant
// - Unlock the door (start listening for requests)
// - Set up security checks at the entrance (authentication middleware)
// - Direct customers to the right tables (routing)
//
// DEPENDENCY INJECTION EXPLAINED SIMPLY:
// Instead of every class creating its own tools with "new SomeClass()"
// we tell the system "here are all the available tools"
// and it automatically gives each class what it needs.
//
// Like a restaurant where the kitchen doesn't buy its own knives —
// the restaurant owns all the tools and provides them as needed.

using Microsoft.EntityFrameworkCore;
using GLMS.Web.Data;
using GLMS.Web.Factories;
using GLMS.Web.Observers;
using GLMS.Web.Repositories;
using GLMS.Web.Services;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Tell the app we are using MVC
// MVC = Model View Controller — the pattern our whole app is built on
// Without this the Controllers and Views wouldnt work
builder.Services.AddControllersWithViews();

// ── Entity Framework Core — SQL Server
// Connection string is read from appsettings.json (never hardcoded)
builder.Services.AddDbContext<GlmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── ASP.NET Core Identity 
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

// ── Repository Pattern Registrations 
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IContractRepository, ContractRepository>();
builder.Services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();

// ── Factory Pattern Registration
builder.Services.AddScoped<IContractFactory, ContractFactory>();

// ── Observer Pattern Registration
builder.Services.AddScoped<AuditLogObserver>();
builder.Services.AddScoped<EmailNotificationObserver>();
builder.Services.AddScoped<IContractService>(sp =>
{
    var service = new ContractService(sp.GetRequiredService<IContractRepository>());
    service.RegisterObserver(sp.GetRequiredService<AuditLogObserver>());
    service.RegisterObserver(sp.GetRequiredService<EmailNotificationObserver>());
    return service;
});

// // REGISTER THE FILE SERVICE
builder.Services.AddHttpClient<ICurrencyService, CurrencyService>();

//  File Service 
builder.Services.AddScoped<IFileService, FileService>();
// "builder.Build()" finalises everything and creates the app object
var app = builder.Build();

//  Auto Migration + Role Seeding
// This code runs ONCE when the app starts
// "using (var scope = ...)" creates a temporary workspace
// It is automatically cleaned up when the { } block ends
using (var scope = app.Services.CreateScope())
{
    // Get our database connection from the toolbox and  Apply any database changes (migrations) automatically

    var db = scope.ServiceProvider.GetRequiredService<GlmsDbContext>();
    // This creates all the tables if they don't exist
    db.Database.Migrate();
    // This creates admin@glms.com, manager@glms.com, viewer@glms.com
    await RoleSeeder.SeedAsync(scope.ServiceProvider);
}

// Serve files from the wwwroot folder — CSS, JavaScript, images
// Without this the website would have no styling or interactive features
app.UseStaticFiles();
app.UseRouting();// This figures out WHICH controller to call based on the URL
app.UseAuthentication(); // Figures out if the user is logged in and who they are
// MUST come BEFORE UseAuthorization — you need to know WHO before checking WHAT

app.UseAuthorization();// CHECK WHAT THE USER CAN DO (Authorization)
// Uses the login information from the step above
// Checks if this user has permission to access this page

app.MapRazorPages();// These are the Login, Register, Logout pages in the Areas folder
// Without this line those pages return "404 Not Found"
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();