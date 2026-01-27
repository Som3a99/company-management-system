using AutoMapper;
using Azure.Core;
using ERP.BLL.Interfaces;
using ERP.BLL.Repositories;
using ERP.DAL.Data.Contexts;
using ERP.PL.Helpers;
using ERP.PL.Mapping.Department;
using ERP.PL.Mapping.Employee;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
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
                // Use HTTPS only in production; allow HTTP in development
                options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
                    ? CookieSecurePolicy.SameAsRequest 
                    : CookieSecurePolicy.Always;
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

            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB
                options.ValueLengthLimit = 10 * 1024 * 1024;
                options.MultipartHeadersLengthLimit = 10 * 1024 * 1024;
            });

            builder.Services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
            });

            builder.Services.Configure<IISServerOptions>(options =>
            {
                options.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
            });
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

            app.UseStatusCodePages(async context =>
            {
                if (context.HttpContext.Response.StatusCode == StatusCodes.Status413PayloadTooLarge)
                {
                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsync(
                        "{\"error\":\"File size exceeds the 10MB limit.\"}"
                    );
                }
            });



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
