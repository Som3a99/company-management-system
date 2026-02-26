using ERP.BLL.Common;
using ERP.BLL.Interfaces;
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
        private readonly ITaskService _taskService;
        private readonly IDashboardIntelligenceService _intelligenceService;

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITaskService taskService,
            IDashboardIntelligenceService intelligenceService)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _taskService = taskService;
            _intelligenceService = intelligenceService;
        }

        // Public homepage - accessible to anyone
        [AllowAnonymous]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
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

            var currentUserId = _userManager.GetUserId(User);
            var visibleTasks = 0;
            var myOpenTasks = 0;

            if (!string.IsNullOrWhiteSpace(currentUserId))
            {
                var visible = await _taskService.GetTasksForUserAsync(new TaskQueryRequest(PageNumber: 1, PageSize: 1), currentUserId);
                var open = await _taskService.GetTasksForUserAsync(new TaskQueryRequest(PageNumber: 1, PageSize: 1, Status: ERP.DAL.Models.TaskStatus.InProgress), currentUserId);
                visibleTasks = visible.TotalCount;
                myOpenTasks = open.TotalCount;
            }

            var viewModel = new HomeDashboardViewModel
            {
                ActiveDepartments = activeDepartments,
                TotalEmployees = totalEmployees,
                ActiveProjects = activeProjects,
                SystemHealthPercentage = Math.Clamp(systemHealth, 0, 100),
                VisibleTasks = visibleTasks,
                MyOpenTasks = myOpenTasks
            };

            // Phase 3 — Proactive Intelligence Widgets
            try
            {
                viewModel.Intelligence = await _intelligenceService.GetIntelligenceAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load dashboard intelligence data");
            }

            return View(viewModel);
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
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
