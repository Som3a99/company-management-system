using ERP.BLL.Reporting.Dtos;
using ERP.BLL.Reporting.Interfaces;
using ERP.DAL.Models;
using ERP.PL.Utilities;
using ERP.PL.ViewModels.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace ERP.PL.Controllers
{
    [Authorize(Policy = "RequireManager")]
    public class ReportingController : Controller
    {
        private readonly IReportingService _reportingService;
        private readonly IReportJobService _reportJobService;

        public ReportingController(IReportingService reportingService, IReportJobService reportJobService)
        {
            _reportingService = reportingService;
            _reportJobService = reportJobService;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] ReportFilterViewModel filters)
        {
            var scope = ResolveScope(filters);
            if (!scope.Allowed)
                return Forbid();

            var request = ToRequest(filters);
            var taskRows = await _reportingService.GetTaskReportAsync(request, scope.DepartmentId, scope.ProjectId);
            var projectRows = await _reportingService.GetProjectReportAsync(request, scope.DepartmentId, scope.ProjectId);

            var vm = new ReportingIndexViewModel
            {
                Filters = filters,
                TaskRows = taskRows,
                ProjectRows = projectRows,
                Widget = new ReportWidgetViewModel
                {
                    TotalTasks = taskRows.Count,
                    OverdueTasks = taskRows.Count(t => t.DueDateUtc.HasValue && t.DueDateUtc.Value < DateTime.UtcNow && !string.Equals(t.Status, ERP.DAL.Models.TaskStatus.Completed.ToString(), StringComparison.OrdinalIgnoreCase)),
                    TotalProjects = projectRows.Count,
                    ActiveProjects = projectRows.Count(p => !string.Equals(p.Status, ProjectStatus.Completed.ToString(), StringComparison.OrdinalIgnoreCase) && !string.Equals(p.Status, ProjectStatus.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase))
                }
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Department([FromQuery] ReportFilterViewModel filters)
        {
            var scope = ResolveScope(filters);
            if (!scope.Allowed || scope.ProjectId.HasValue)
                return Forbid();

            var rows = await _reportingService.GetDepartmentReportAsync(ToRequest(filters), scope.DepartmentId);
            return View(new ReportingIndexViewModel
            {
                Filters = filters,
                DepartmentRows = rows
            });
        }

        [Authorize(Policy = "RequireCEO")]
        [HttpGet]
        public async Task<IActionResult> Audit([FromQuery] ReportFilterViewModel filters)
        {
            var rows = await _reportingService.GetAuditReportAsync(ToRequest(filters));
            return View(new ReportingIndexViewModel
            {
                Filters = filters,
                AuditRows = rows
            });
        }

        [HttpGet]
        public async Task<IActionResult> Jobs()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            var jobs = await _reportJobService.GetUserJobsAsync(userId);
            var presets = await _reportJobService.GetUserPresetsAsync(userId);
            return View(new ReportJobsViewModel { Jobs = jobs, Presets = presets });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QueueJob(ReportJobRequestViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            var scope = ResolveScope(model.Filters);
            if (!scope.Allowed)
                return Forbid();

            var reportType = model.ReportType;
            if (reportType == ReportType.Audit && !User.IsInRole("CEO"))
                return Forbid();

            if (reportType == ReportType.Departments && scope.ProjectId.HasValue)
                return Forbid();

            await _reportJobService.EnqueueJobAsync(userId, reportType, model.Format, ToScopedRequest(model.Filters, scope));
            TempData["SuccessMessage"] = "Report job queued. Refresh Jobs page in a few seconds.";
            return RedirectToAction(nameof(Jobs));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePreset(ReportPresetCreateViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            var scope = ResolveScope(model.Filters);
            if (!scope.Allowed)
                return Forbid();

            if (model.ReportType == ReportType.Audit && !User.IsInRole("CEO"))
                return Forbid();

            if (model.ReportType == ReportType.Departments && scope.ProjectId.HasValue)
                return Forbid();

            await _reportJobService.SavePresetAsync(userId, model.Name.Trim(), model.ReportType, ToScopedRequest(model.Filters, scope));
            TempData["SuccessMessage"] = "Preset saved successfully.";
            return RedirectToAction(nameof(Jobs));
        }

        [HttpGet]
        public async Task<IActionResult> LoadPreset(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            var preset = (await _reportJobService.GetUserPresetsAsync(userId)).FirstOrDefault(p => p.Id == id);
            if (preset == null)
                return NotFound();

            var filters = JsonSerializer.Deserialize<ReportFilterViewModel>(preset.FiltersJson) ?? new ReportFilterViewModel();

            return preset.ReportType switch
            {
                ReportType.Departments => RedirectToAction(nameof(Department), filters),
                ReportType.Audit when User.IsInRole("CEO") => RedirectToAction(nameof(Audit), filters),
                _ => RedirectToAction(nameof(Index), filters)
            };
        }

        [HttpGet]
        public async Task<IActionResult> ExportTasks([FromQuery] ReportFilterViewModel filters)
        {
            var scope = ResolveScope(filters);
            if (!scope.Allowed)
                return Forbid();

            var rows = await _reportingService.GetTaskReportAsync(ToScopedRequest(filters, scope), scope.DepartmentId, scope.ProjectId);
            var headers = new[] { "Task Id", "Title", "Status", "Priority", "Project", "Assignee", "Due (UTC)", "Estimated Hours", "Actual Hours" };
            var data = rows.Select(r => (IReadOnlyList<string?>)[r.TaskId.ToString(), r.Title, r.Status, r.Priority, r.Project, r.Assignee, r.DueDateUtc?.ToString("u"), r.EstimatedHours?.ToString("0.##"), r.ActualHours.ToString("0.##")]).ToList();
            return BuildExportResult(filters.Export, "Tasks Report", "tasks-report", headers, data);
        }

        [HttpGet]
        public async Task<IActionResult> ExportProjects([FromQuery] ReportFilterViewModel filters)
        {
            var scope = ResolveScope(filters);
            if (!scope.Allowed)
                return Forbid();

            var rows = await _reportingService.GetProjectReportAsync(ToScopedRequest(filters, scope), scope.DepartmentId, scope.ProjectId);
            var headers = new[] { "Project Id", "Project Code", "Project Name", "Department", "Status", "Budget", "Completed Tasks", "Total Tasks" };
            var data = rows.Select(r => (IReadOnlyList<string?>)[r.ProjectId.ToString(), r.ProjectCode, r.ProjectName, r.DepartmentName, r.Status, r.Budget.ToString("0.##"), r.CompletedTasks.ToString(), r.TotalTasks.ToString()]).ToList();
            return BuildExportResult(filters.Export, "Projects Report", "projects-report", headers, data);
        }

        [HttpGet]
        public async Task<IActionResult> ExportDepartments([FromQuery] ReportFilterViewModel filters)
        {
            var scope = ResolveScope(filters);
            if (!scope.Allowed || scope.ProjectId.HasValue)
                return Forbid();

            var rows = await _reportingService.GetDepartmentReportAsync(ToScopedRequest(filters, scope), scope.DepartmentId);
            var headers = new[] { "Department Id", "Department Code", "Department Name", "Employees", "Projects", "Open Tasks" };
            var data = rows.Select(r => (IReadOnlyList<string?>)[r.DepartmentId.ToString(), r.DepartmentCode, r.DepartmentName, r.EmployeesCount.ToString(), r.ProjectsCount.ToString(), r.OpenTasksCount.ToString()]).ToList();
            return BuildExportResult(filters.Export, "Departments Report", "departments-report", headers, data);
        }

        [Authorize(Policy = "RequireCEO")]
        [HttpGet]
        public async Task<IActionResult> ExportAudit([FromQuery] ReportFilterViewModel filters)
        {
            var rows = await _reportingService.GetAuditReportAsync(ToRequest(filters));
            var headers = new[] { "Timestamp (UTC)", "User Email", "Action", "Resource Type", "Resource Id", "Succeeded", "Error" };
            var data = rows.Select(r => (IReadOnlyList<string?>)[r.TimestampUtc.ToString("u"), r.UserEmail, r.Action, r.ResourceType, r.ResourceId?.ToString(), r.Succeeded.ToString(), r.ErrorMessage]).ToList();
            return BuildExportResult(filters.Export, "Audit Activity Report", "audit-report", headers, data);
        }

        private ReportRequestDto ToRequest(ReportFilterViewModel vm) => new()
        {
            StartDateUtc = vm.StartDateUtc,
            EndDateUtc = vm.EndDateUtc,
            DepartmentId = vm.DepartmentId,
            ProjectId = vm.ProjectId
        };

        private ReportRequestDto ToScopedRequest(ReportFilterViewModel vm, (bool Allowed, int? DepartmentId, int? ProjectId) scope)
        {
            var request = ToRequest(vm);
            request.DepartmentId = scope.DepartmentId ?? request.DepartmentId;
            request.ProjectId = scope.ProjectId ?? request.ProjectId;
            return request;
        }

        private (bool Allowed, int? DepartmentId, int? ProjectId) ResolveScope(ReportFilterViewModel filters)
        {
            if (User.IsInRole("CEO"))
                return (true, null, null);

            if (User.IsInRole("DepartmentManager"))
            {
                var managedDepartmentId = GetIntClaim("ManagedDepartmentId");
                if (!managedDepartmentId.HasValue)
                    return (false, null, null);

                if (filters.DepartmentId.HasValue && filters.DepartmentId.Value != managedDepartmentId.Value)
                    return (false, null, null);

                return (true, managedDepartmentId.Value, null);
            }

            if (User.IsInRole("ProjectManager"))
            {
                var managedProjectId = GetIntClaim("ManagedProjectId");
                if (!managedProjectId.HasValue)
                    return (false, null, null);

                if (filters.ProjectId.HasValue && filters.ProjectId.Value != managedProjectId.Value)
                    return (false, null, null);

                return (true, null, managedProjectId.Value);
            }

            return (false, null, null);
        }

        private int? GetIntClaim(string claimType)
        {
            var value = User.FindFirstValue(claimType);
            if (int.TryParse(value, out var parsed))
                return parsed;

            return null;
        }

        private IActionResult BuildExportResult(ReportExportFormat format, string title, string name, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
        {
            return format switch
            {
                ReportExportFormat.Csv => File(ReportExportUtility.ToCsv(title, headers, rows), "text/csv", $"{name}.csv"),
                ReportExportFormat.Excel => File(ReportExportUtility.ToExcel(headers, rows, title), "application/vnd.ms-excel", $"{name}.xls"),
                ReportExportFormat.Pdf => File(ReportExportUtility.ToPdf(title, headers, rows), "application/pdf", $"{name}.pdf"),
                _ => File(ReportExportUtility.ToCsv(title, headers, rows), "text/csv", $"{name}.csv")
            };
        }
    }
}
