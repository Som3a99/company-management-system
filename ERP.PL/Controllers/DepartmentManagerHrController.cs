using ERP.PL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ERP.PL.Controllers
{
    [Authorize(Policy = "RequireDepartmentManager")]
    public class DepartmentManagerHrController : Controller
    {

        private readonly IDepartmentManagerHrService _departmentManagerHrService;

        public DepartmentManagerHrController(IDepartmentManagerHrService departmentManagerHrService)
        {
            _departmentManagerHrService = departmentManagerHrService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var managerDepartmentId = GetManagerDepartmentId();
            if (!managerDepartmentId.HasValue)
            {
                return Forbid();
            }

            var departmentEmployees = await _departmentManagerHrService
                .GetDepartmentEmployeesAsync(managerDepartmentId.Value, cancellationToken);
            var unassignedEmployees = await _departmentManagerHrService
                .GetUnassignedEmployeesAsync(cancellationToken);

            ViewBag.DepartmentEmployees = departmentEmployees;
            ViewBag.UnassignedEmployees = unassignedEmployees;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(int employeeId, CancellationToken cancellationToken)
        {
            var managerDepartmentId = GetManagerDepartmentId();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.Identity?.Name;

            if (!managerDepartmentId.HasValue || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(userEmail))
            {
                return Forbid();
            }

            var result = await _departmentManagerHrService.AssignEmployeeAsync(
                employeeId,
                managerDepartmentId.Value,
                userId,
                userEmail,
                cancellationToken);

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int employeeId, CancellationToken cancellationToken)
        {
            var managerDepartmentId = GetManagerDepartmentId();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.Identity?.Name;

            if (!managerDepartmentId.HasValue || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(userEmail))
            {
                return Forbid();
            }

            var result = await _departmentManagerHrService.RemoveEmployeeAsync(
                employeeId,
                managerDepartmentId.Value,
                userId,
                userEmail,
                cancellationToken);

            if (!result.Succeeded && result.Message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized();
            }

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        private int? GetManagerDepartmentId()
        {
            var claimValue = User.FindFirstValue("ManagedDepartmentId");
            if (int.TryParse(claimValue, out var departmentId))
            {
                return departmentId;
            }

            return null;
        }
    }
}
