using ERP.DAL.Models;
using ERP.PL.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ERP.PL.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        // Public homepage - accessible to anyone
        [AllowAnonymous]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
        public IActionResult Index()
        {
            // If user is already logged in, redirect to role-specific home
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToRoleHome();
            }

            return View("Index");
        }

        /// <summary>
        /// Redirects authenticated users to their role-specific home dashboard.
        /// Priority: CEO > ITAdmin > DepartmentManager > ProjectManager > Employee
        /// </summary>
        private IActionResult RedirectToRoleHome()
        {
            if (User.IsInRole("CEO"))
                return RedirectToAction("Index", "ExecutiveHome");

            if (User.IsInRole("ITAdmin"))
                return RedirectToAction("Index", "ITAdminHome");

            if (User.IsInRole("DepartmentManager") || User.IsInRole("ProjectManager"))
                return RedirectToAction("Index", "ManagerHome");

            return RedirectToAction("Index", "EmployeeHome");
        }

        // Legacy dashboard — redirects to role-specific home
        [Authorize]
        public IActionResult Dashboard()
        {
            return RedirectToRoleHome();
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
