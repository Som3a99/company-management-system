using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using ERP.PL.ViewModels;
using ERP.PL.ViewModels.Home;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace ERP.PL.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _context=context;
            _userManager=userManager;
        }

        // Public homepage - accessible to anyone
        [AllowAnonymous]
        public IActionResult Index()
        {
            // If user is already logged in, redirect to internal dashboard
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Dashboard");
            }

            return View("Index");
        }

        // Internal dashboard - requires authentication
        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            var activeDepartments = await _context.Departments.CountAsync();
            var totalEmployees = await _context.Employees.CountAsync();
            var activeProjects = await _context.Projects
                .Where(p => p.Status != ProjectStatus.Completed && p.Status != ProjectStatus.Cancelled)
                .CountAsync();

            var totalUserAccounts = await _userManager.Users.CountAsync();
            var activeHealthyAccounts = await _userManager.Users
                .Where(u => u.IsActive && (u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow))
                .CountAsync();

            var systemHealth = totalUserAccounts == 0
                ? 100
                : (int)Math.Round((activeHealthyAccounts / (double)totalUserAccounts) * 100, MidpointRounding.AwayFromZero);

            var viewModel = new HomeDashboardViewModel
            {
                ActiveDepartments = activeDepartments,
                TotalEmployees = totalEmployees,
                ActiveProjects = activeProjects,
                SystemHealthPercentage = Math.Clamp(systemHealth, 0, 100)
            };

            return View(viewModel);
        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
