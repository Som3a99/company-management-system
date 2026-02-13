using ERP.BLL.Reporting.Dtos;
using ERP.BLL.Reporting.Interfaces;
using ERP.PL.Utilities;
using ERP.PL.ViewModels.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ERP.PL.Controllers
{
    [Authorize(Policy = "RequireManager")]
    public class ReportingController : Controller
    {
        private readonly IReportingService _reportingService;

        public ReportingController(IReportingService reportingService)
        {
            _reportingService = reportingService;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] ReportFilterViewModel filters)
        {
            var scope = ResolveScope(filters);
            if (!scope.Allowed)
                return Forbid();

            var request = ToRequest(filters);
            var vm = new ReportingIndexViewModel
            {
                Filters = filters,
                TaskRows = await _reportingService.GetTaskReportAsync(request, scope.DepartmentId, scope.ProjectId),
                ProjectRows = await _reportingService.GetProjectReportAsync(request, scope.DepartmentId, scope.ProjectId)
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Department([FromQuery] ReportFilterViewModel filters)
        {
            var scope = ResolveScope(filters);
            if (!scope.Allowed || scope.ProjectId.HasValue)
                return Forbid();

            var vm = new ReportingIndexViewModel
            {
                Filters = filters,
                DepartmentRows = await _reportingService.GetDepartmentReportAsync(ToRequest(filters), scope.DepartmentId)
            };

            return View("Department", vm);
        }

        [Authorize(Policy = "RequireCEO")]
        [HttpGet]
        public async Task<IActionResult> Audit([FromQuery] ReportFilterViewModel filters)
        {
            var vm = new ReportingIndexViewModel
            {
                Filters = filters,
                AuditRows = await _reportingService.GetAuditReportAsync(ToRequest(filters))
            };

            return View("Audit", vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportTasks([FromQuery] ReportFilterViewModel filters)
        {
            var scope = ResolveScope(filters);
            if (!scope.Allowed)
                return Forbid();

            var rows = await _reportingService.GetTaskReportAsync(ToRequest(filters), scope.DepartmentId, scope.ProjectId);

            var headers = new[] { "TaskId", "Title", "Status", "Priority", "Project", "Assignee", "DueDateUtc", "EstimatedHours", "ActualHours" };
            var data = rows.Select(r => (IReadOnlyList<string?>)
            [
                r.TaskId.ToString(),
            r.Title,
            r.Status,
            r.Priority,
            r.Project,
            r.Assignee,
            r.DueDateUtc?.ToString("u"),
            r.EstimatedHours?.ToString(),
            r.ActualHours.ToString()
            ]).ToList();

            return BuildExportResult(filters.Export, "tasks-report", headers, data);
        }

        [HttpGet]
        public async Task<IActionResult> ExportProjects([FromQuery] ReportFilterViewModel filters)
        {
            var scope = ResolveScope(filters);
            if (!scope.Allowed)
                return Forbid();

            var rows = await _reportingService.GetProjectReportAsync(ToRequest(filters), scope.DepartmentId, scope.ProjectId);

            var headers = new[] { "ProjectId", "ProjectCode", "ProjectName", "Department", "Status", "Budget", "TotalTasks", "CompletedTasks" };
            var data = rows.Select(r => (IReadOnlyList<string?>)
            [
                r.ProjectId.ToString(),
            r.ProjectCode,
            r.ProjectName,
            r.DepartmentName,
            r.Status,
            r.Budget.ToString(),
            r.TotalTasks.ToString(),
            r.CompletedTasks.ToString()
            ]).ToList();

            return BuildExportResult(filters.Export, "projects-report", headers, data);
        }

        private ReportRequestDto ToRequest(ReportFilterViewModel vm) => new()
        {
            StartDateUtc = vm.StartDateUtc,
            EndDateUtc = vm.EndDateUtc,
            DepartmentId = vm.DepartmentId,
            ProjectId = vm.ProjectId
        };

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

        private IActionResult BuildExportResult(ReportExportFormat format, string name, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
        {
            return format switch
            {
                ReportExportFormat.Csv => File(ReportExportUtility.ToCsv(headers, rows), "text/csv", $"{name}.csv"),
                ReportExportFormat.Excel => File(ReportExportUtility.ToExcelTsv(headers, rows), "application/vnd.ms-excel", $"{name}.xls"),
                ReportExportFormat.Pdf => File(ReportExportUtility.ToPdfText(name, headers, rows), "application/pdf", $"{name}.pdf"),
                _ => File(ReportExportUtility.ToCsv(headers, rows), "text/csv", $"{name}.csv")
            };
        }
    }
}
