using hotel.Models;
using Microsoft.AspNetCore.Identity;

namespace hotel.Data
{
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, bool isDevelopment)
        {
            using var scope = services.CreateScope();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(IdentitySeeder));

            foreach (var name in new[] { "Admin", "Customer" })
            {
                if (await roles.RoleExistsAsync(name)) continue;
                var result = await roles.CreateAsync(new IdentityRole(name));
                if (!result.Succeeded && !await roles.RoleExistsAsync(name))
                    throw new InvalidOperationException($"Could not initialize the {name} role.");
            }

            if (!isDevelopment) return;
            var email = configuration["DevelopmentAdmin:Email"]?.Trim();
            var password = configuration["DevelopmentAdmin:Password"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                logger.LogInformation("Development Admin creation skipped. Configure DevelopmentAdmin:Email and DevelopmentAdmin:Password in User Secrets.");
                return;
            }

            var existing = await users.FindByEmailAsync(email) ?? await users.FindByNameAsync(email);
            if (existing != null)
            {
                if (!await users.IsInRoleAsync(existing, "Admin"))
                    logger.LogWarning("Development Admin creation skipped: the configured account already exists without Admin membership. Existing customer accounts are never promoted by the seeder.");
                return;
            }

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await using var transaction = await context.Database.BeginTransactionAsync();
            var admin = new ApplicationUser { Name = "Development Admin", UserName = email, Email = email };
            var created = await users.CreateAsync(admin, password);
            if (!created.Succeeded)
            {
                logger.LogWarning("Development Admin creation skipped. Check the configured email and Identity password requirements. Error codes: {Codes}",
                    string.Join(", ", created.Errors.Select(e => e.Code)));
                return;
            }
            var assigned = await users.AddToRoleAsync(admin, "Admin");
            if (!assigned.Succeeded)
            {
                logger.LogWarning("Development Admin creation rolled back because role assignment failed.");
                return;
            }
            await transaction.CommitAsync();
            logger.LogInformation("Development Admin account created.");
        }
    }
}
