using AutoMapper;
using Azure.Core;
using ERP.BLL.Interfaces;
using ERP.BLL.Repositories;
using ERP.DAL.Data.Contexts;
using ERP.PL.Helpers;
using ERP.PL.Mapping.Department;
using ERP.PL.Mapping.Employee;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Protocol.Core.Types;
using System.Runtime.ConstrainedExecution;
using static System.Formats.Asn1.AsnWriter;
namespace ERP.PL
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            #region Configure Services
            // Add services to the container.
            builder.Services.AddControllersWithViews();

            // Database Context - Scoped lifetime
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
                // Enable sensitive data logging in development only
                if (builder.Environment.IsDevelopment())
                {
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                }
            });

            // Repository Pattern -Scoped lifetime(one per request)
            builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
            builder.Services.AddScoped<IDepartmentRepository, DepartmentRepository>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Auto Mapper Configurations
            builder.Services.AddAutoMapper(cfg => { }, typeof(EmployeeProfile).Assembly);
            builder.Services.AddAutoMapper(cfg => { }, typeof(DepartmentProfile).Assembly);

            // Document Settings
            builder.Services.AddScoped<DocumentSettings>();

            // Input Sanitizer - Singleton (stateless utility)
            builder.Services.AddSingleton<InputSanitizer>();

            //  Add Anti-forgery token validation
            builder.Services.AddAntiforgery(options =>
            {
                options.HeaderName = "X-CSRF-TOKEN";
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only in production
                options.Cookie.SameSite = SameSiteMode.Strict;
            });

            //  Add response compression
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
            });

            // LOGGING: Configure logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();

            if (builder.Environment.IsDevelopment())
            {
                builder.Logging.SetMinimumLevel(LogLevel.Information);
            }
            else
            {
                builder.Logging.SetMinimumLevel(LogLevel.Warning);
            }

            #endregion

            var app = builder.Build();

            #region Configure Kestral Middelware
            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            // PERFORMANCE: Enable response compression
            app.UseResponseCompression();

            app.UseStaticFiles();

            app.UseRouting();


            // SECURITY: Add authentication/authorization (when implemented)
            // app.UseAuthentication();
            // app.UseAuthorization();


            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
            #endregion

            app.Run();
        }
    }
}
