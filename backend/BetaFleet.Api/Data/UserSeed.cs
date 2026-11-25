using BetaFleet.Api.Domain;
using BetaFleet.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BetaFleet.Api.Data;

/// Creates the first accounts on an empty Users table.
/// Demo: one account per role, all with Seed:DemoPassword. Production: only "admin" with Auth:AdminPassword, which must be changed on first sign-in.
public static class UserSeed
{
    public static async Task RunAsync(AppDb db, IPasswordHasher<User> hasher, IConfiguration cfg, ILogger log)
    {
        if (await db.Users.AnyAsync()) return;
        var demo = cfg["Seed:DemoPassword"];
        var admin = cfg["Auth:AdminPassword"];

        if (cfg.GetValue("Seed:SampleData", true) && !string.IsNullOrWhiteSpace(demo))
        {
            AuthSetup.ValidatePassword(demo);
            var kmc = await db.Customers.FirstOrDefaultAsync(c => c.Name == "Kilimanjaro Mining Company");
            var evd = await db.Customers.FirstOrDefaultAsync(c => c.Name == "Everdigitio Ltd");
            (string User, string Name, string Role, int? Cust)[] accounts =
            [
                ("admin", "Administrator", Roles.Admin, null),
                ("jkimaro", "J. Kimaro", Roles.Clerk, null),
                ("smassawe", "S. Massawe", Roles.OpsManager, null),
                ("asaid", "A. Said", Roles.UnitHead, null),
                ("mjuma", "M. Juma", Roles.UnitHead, null),
                ("lmrema", "L. Mrema", Roles.Finance, null),
                ("jmollel", "J. Mollel", Roles.Workshop, null),
                ("nkisanga", "N. Kisanga", Roles.Storekeeper, null),
                ("pbanda", "P. Banda", Roles.Driver, null),
                ("sngowi", "S. Ngowi", Roles.Driver, null),
                ("kmc.portal", "KMC Logistics Desk", Roles.Customer, kmc?.Id),
                ("everdigitio.portal", "Everdigitio Imports", Roles.Customer, evd?.Id)
            ];
            foreach (var a in accounts.Where(a => a.Role != Roles.Customer || a.Cust is not null))
            {
                var u = new User { Username = a.User, DisplayName = a.Name, Role = a.Role, CustomerId = a.Cust, PasswordHash = "", CreatedAt = DateTimeOffset.UtcNow };
                u.PasswordHash = hasher.HashPassword(u, demo);
                db.Users.Add(u);
            }
            await db.SaveChangesAsync();
            log.LogInformation("Created {Count} demo accounts (password from Seed:DemoPassword).", accounts.Length);
        }
        else if (!string.IsNullOrWhiteSpace(admin))
        {
            AuthSetup.ValidatePassword(admin);
            var u = new User { Username = "admin", DisplayName = "Administrator", Role = Roles.Admin, PasswordHash = "", MustChangePassword = true, CreatedAt = DateTimeOffset.UtcNow };
            u.PasswordHash = hasher.HashPassword(u, admin);
            db.Users.Add(u);
            await db.SaveChangesAsync();
            log.LogInformation("Created bootstrap admin account; it must change its password on first sign-in.");
        }
        else log.LogWarning("No user accounts exist. Set ADMIN_PASSWORD (Auth:AdminPassword) to create the first admin.");
    }
}
