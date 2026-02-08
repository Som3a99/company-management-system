using ERP.DAL.Models;
using Microsoft.AspNetCore.Identity;

namespace ERP.PL.Data
{
    /// <summary>
    /// Seeds initial roles and CEO admin account
    /// Runs on application startup (in development and production)
    /// </summary>
    public static class IdentitySeeder
    {
        /// <summary>
        /// Seeds all required data for Identity to function
        /// </summary>
        public static async Task SeedAsync(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger logger)
        {
            try
            {
                // 1. Seed roles first (required for user creation)
                await SeedRolesAsync(roleManager, logger);

                // 2. Seed CEO admin account
                await SeedCEOAsync(userManager, logger);

                logger.LogInformation("Identity seeding completed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during Identity seeding");
                throw; // Fail fast - app can't run without these
            }
        }

        /// <summary>
        /// Seeds the five core roles
        /// </summary>
        private static async Task SeedRolesAsync(
            RoleManager<IdentityRole> roleManager,
            ILogger logger)
        {
            string[] roles =
            {
                "CEO",              // System administrator
                "ITAdmin",          // Technical support, user management
                "DepartmentManager",// Manages a specific department
                "ProjectManager",   // Manages a specific project
                "Employee"          // Base role for all users
            };

            foreach (var roleName in roles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    var result = await roleManager.CreateAsync(new IdentityRole(roleName));

                    if (result.Succeeded)
                    {
                        logger.LogInformation($"Created role: {roleName}");
                    }
                    else
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        logger.LogError($"Failed to create role {roleName}: {errors}");
                        throw new Exception($"Role creation failed: {errors}");
                    }
                }
            }
        }

        /// <summary>
        /// Seeds the default CEO admin account
        /// WARNING: Change password immediately after first login!
        /// </summary>
        private static async Task SeedCEOAsync(
            UserManager<ApplicationUser> userManager,
            ILogger logger)
        {
            const string adminEmail = "ceo@company.com";
            const string defaultPassword = "CEO@Admin123!"; // MUST CHANGE ON FIRST LOGIN

            // Check if CEO already exists
            var existingCEO = await userManager.FindByEmailAsync(adminEmail);
            if (existingCEO != null)
            {
                logger.LogInformation($"CEO account already exists: {adminEmail}");
                return;
            }

            // Create CEO account
            var ceoUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true, // Skip email confirmation for admin
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                EmployeeId = null, // Pure IT account, no employee record
                RequirePasswordChange = true // Force change on first login
            };

            var createResult = await userManager.CreateAsync(ceoUser, defaultPassword);

            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                logger.LogError($"Failed to create CEO account: {errors}");
                throw new Exception($"CEO account creation failed: {errors}");
            }

            // Assign CEO role
            var roleResult = await userManager.AddToRoleAsync(ceoUser, "CEO");

            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                logger.LogError($"Failed to assign CEO role: {errors}");
                throw new Exception($"Role assignment failed: {errors}");
            }

            logger.LogWarning($"Created CEO account: {adminEmail} with default password");
            logger.LogWarning("SECURITY: Change CEO password immediately!");
        }
    }
}
