using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Tests.Infrastructure
{
    public class TestWebApplicationFactory : WebApplicationFactory<ERP.PL.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
                services.RemoveAll(typeof(ApplicationDbContext));

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase($"erp-tests-{Guid.NewGuid():N}"));

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.AuthScheme;
                    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultForbidScheme = TestAuthHandler.AuthScheme;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.AuthScheme, _ => { });

                // Antiforgery cookie must allow HTTP because the test host is non-HTTPS
                services.PostConfigure<AntiforgeryOptions>(opts =>
                    opts.Cookie.SecurePolicy = CookieSecurePolicy.None);

                // Identity application cookie must also allow HTTP for the test host
                services.PostConfigure<CookieAuthenticationOptions>(
                    IdentityConstants.ApplicationScheme,
                    opts => opts.Cookie.SecurePolicy = CookieSecurePolicy.None);

                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Seed(db);
            });
        }

        private static void Seed(ApplicationDbContext db)
        {
            var d1 = new Department { Id = 1, DepartmentCode = "DEP_001", DepartmentName = "Engineering", CreatedAt = DateTime.UtcNow };
            var d2 = new Department { Id = 2, DepartmentCode = "DEP_002", DepartmentName = "Finance", CreatedAt = DateTime.UtcNow };
            db.Departments.AddRange(d1, d2);

            db.Projects.AddRange(
                new Project { Id = 1, DepartmentId = 1, ProjectCode = "PRJ_001", ProjectName = "ERP Core", StartDate = DateTime.UtcNow, Status = ProjectStatus.InProgress },
                new Project { Id = 2, DepartmentId = 2, ProjectCode = "PRJ_002", ProjectName = "Finance Portal", StartDate = DateTime.UtcNow, Status = ProjectStatus.Planning });

            db.SaveChanges();
        }

        /// <summary>
        /// Seed department records into a standalone DbContext for repository-level tests.
        /// </summary>
        public static void SeedDepartments(ApplicationDbContext db)
        {
            if (!db.Departments.Any())
            {
                db.Departments.AddRange(
                    new Department { Id = 1, DepartmentCode = "DEP_001", DepartmentName = "Engineering", CreatedAt = DateTime.UtcNow },
                    new Department { Id = 2, DepartmentCode = "DEP_002", DepartmentName = "Finance", CreatedAt = DateTime.UtcNow });
                db.SaveChanges();
            }
        }
    }
}