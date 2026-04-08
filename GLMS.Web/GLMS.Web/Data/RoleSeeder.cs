// ─── ROLE SEEDER 
// Automatically creates the Admin, Manager and Viewer roles in the database
// on startup. Also creates default accounts for each role so you can
// log in and test immediately.
//
// ROLES:
// - Admin:   Full access — search/filter, manage everything
// - Manager: Can create contracts and raise service requests
// - Viewer:  Read-only access — can only view data
//
// DEFAULT CREDENTIALS:
// Admin:   admin@glms.com   / Admin123
// Manager: manager@glms.com / Manager123
// Viewer:  viewer@glms.com  / Viewer123

using Microsoft.AspNetCore.Identity;

namespace GLMS.Web.Data
{
    public static class RoleSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

            // ── Create Roles if they don't exist ──────────────────────────────
            string[] roles = { "Admin", "Manager", "Viewer" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // ── Default Admin account ─────────────────────────────────────────
            await CreateUserAsync(userManager,
                "admin@glms.com", "Admin123", "Admin");

            // ── Default Manager account ───────────────────────────────────────
            await CreateUserAsync(userManager,
                "manager@glms.com", "Manager123", "Manager");

            // ── Default Viewer account ────────────────────────────────────────
            await CreateUserAsync(userManager,
                "viewer@glms.com", "Viewer123", "Viewer");
        }

        // Helper method — creates a user and assigns a role if they don't exist
        private static async Task CreateUserAsync(UserManager<IdentityUser> userManager,
            string email, string password, string role)
        {
            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser == null)
            {
                var user = new IdentityUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(user, role);
            }
        }
    }
}