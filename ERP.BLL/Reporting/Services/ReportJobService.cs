using ERP.BLL.Reporting.Dtos;
using ERP.BLL.Reporting.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ERP.BLL.Reporting.Services
{
    public sealed class ReportJobService : IReportJobService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IReportingService _reportingService;
        private readonly string _webRootPath;
        private readonly ILogger<ReportJobService> _logger;

        public ReportJobService(ApplicationDbContext dbContext, IReportingService reportingService, ILogger<ReportJobService> logger, string? webRootPath = null)
        {
            _dbContext = dbContext;
            _reportingService = reportingService;
            _logger = logger;
            // Use provided webRootPath, or fall back to ContentRootPath/wwwroot
            _webRootPath = webRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        }

        public async Task<int> EnqueueJobAsync(string userId, ReportType reportType, ReportFileFormat format, ReportRequestDto request)
        {
            var job = new ReportJob
            {
                RequestedByUserId = userId,
                ReportType = reportType,
                Format = format,
                FiltersJson = JsonSerializer.Serialize(request),
                Status = ReportJobStatus.Pending,
                RequestedAtUtc = DateTime.UtcNow
            };

            _dbContext.ReportJobs.Add(job);
            await _dbContext.SaveChangesAsync();
            return job.Id;
        }

        public async Task<IReadOnlyList<ReportJob>> GetUserJobsAsync(string userId)
        {
            return await _dbContext.ReportJobs
                .AsNoTracking()
                .Where(j => j.RequestedByUserId == userId)
                .OrderByDescending(j => j.RequestedAtUtc)
                .Take(100)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<ReportPreset>> GetUserPresetsAsync(string userId)
        {
            return await _dbContext.ReportPresets
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAtUtc)
                .Take(100)
                .ToListAsync();
        }

        public async Task SavePresetAsync(string userId, string name, ReportType reportType, ReportRequestDto request)
        {
            var existing = await _dbContext.ReportPresets
                .FirstOrDefaultAsync(p => p.UserId == userId && p.ReportType == reportType && p.Name == name);

            if (existing == null)
            {
                _dbContext.ReportPresets.Add(new ReportPreset
                {
                    UserId = userId,
                    Name = name,
                    ReportType = reportType,
                    FiltersJson = JsonSerializer.Serialize(request),
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                existing.FiltersJson = JsonSerializer.Serialize(request);
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task<bool> ProcessNextPendingJobAsync(CancellationToken cancellationToken)
        {
            var job = await _dbContext.ReportJobs
                .OrderBy(j => j.RequestedAtUtc)
                .FirstOrDefaultAsync(j => j.Status == ReportJobStatus.Pending, cancellationToken);

            if (job == null)
                return false;

            job.Status = ReportJobStatus.Processing;
            job.StartedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                var request = string.IsNullOrWhiteSpace(job.FiltersJson)
                    ? new ReportRequestDto()
                    : JsonSerializer.Deserialize<ReportRequestDto>(job.FiltersJson) ?? new ReportRequestDto();

                var payload = await BuildPayloadAsync(job.ReportType, request);
                var bytes = job.Format switch
                {
                    ReportFileFormat.Csv => ReportFileBuilder.ToCsv(payload.Title, payload.Headers, payload.Rows),
                    ReportFileFormat.Excel => ReportFileBuilder.ToExcelHtml(payload.Title, payload.Headers, payload.Rows),
                    ReportFileFormat.Pdf => ReportFileBuilder.ToSimplePdf(payload.Title, payload.Headers, payload.Rows),
                    _ => ReportFileBuilder.ToSimplePdf(payload.Title, payload.Headers, payload.Rows)
                };

                var ext = job.Format switch
                {
                    ReportFileFormat.Csv => "csv",
                    ReportFileFormat.Excel => "xlsx",
                    ReportFileFormat.Pdf => "pdf",
                    _ => "pdf"
                };

                var folder = Path.Combine(_webRootPath, "reports", "jobs");
                Directory.CreateDirectory(folder);
                var fileName = $"report-job-{job.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}.{ext}";
                var fullPath = Path.Combine(folder, fileName);
                await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);

                _logger.LogInformation("Report job {JobId} written to {FullPath} ({ByteCount} bytes)", job.Id, fullPath, bytes.Length);

                job.OutputPath = $"/reports/jobs/{fileName}";
                job.Status = ReportJobStatus.Completed;
                job.CompletedAtUtc = DateTime.UtcNow;
                job.FailureReason = null;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                job.Status = ReportJobStatus.Failed;
                job.CompletedAtUtc = DateTime.UtcNow;
                job.FailureReason = ex.Message;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return true;
        }

        private async Task<(string Title, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string?>> Rows)> BuildPayloadAsync(ReportType reportType, ReportRequestDto request)
        {
            if (reportType == ReportType.Tasks)
            {
                var rows = await _reportingService.GetTaskReportAsync(request, null, null);
                return (
                    "Tasks Report",
                    ["Task Id", "Title", "Status", "Priority", "Project", "Assignee", "Due (UTC)", "Est. Hours", "Actual Hours"],
                    rows.Select(r => (IReadOnlyList<string?>)[r.TaskId.ToString(), r.Title, r.Status, r.Priority, r.Project, r.Assignee, r.DueDateUtc?.ToString("u"), r.EstimatedHours?.ToString("0.##"), r.ActualHours.ToString("0.##")]).ToList()
                );
            }

            if (reportType == ReportType.Projects)
            {
                var rows = await _reportingService.GetProjectReportAsync(request, null, null);
                return (
                    "Projects Report",
                    ["Project Id", "Code", "Name", "Department", "Status", "Budget", "Completed Tasks", "Total Tasks"],
                    rows.Select(r => (IReadOnlyList<string?>)[r.ProjectId.ToString(), r.ProjectCode, r.ProjectName, r.DepartmentName, r.Status, r.Budget.ToString("0.##"), r.CompletedTasks.ToString(), r.TotalTasks.ToString()]).ToList()
                );
            }

            if (reportType == ReportType.Departments)
            {
                var rows = await _reportingService.GetDepartmentReportAsync(request, null);
                return (
                    "Departments Report",
                    ["Department Id", "Code", "Name", "Employees", "Projects", "Open Tasks"],
                    rows.Select(r => (IReadOnlyList<string?>)[r.DepartmentId.ToString(), r.DepartmentCode, r.DepartmentName, r.EmployeesCount.ToString(), r.ProjectsCount.ToString(), r.OpenTasksCount.ToString()]).ToList()
                );
            }

            var audits = await _reportingService.GetAuditReportAsync(request);
            return (
                "Audit Activity Report",
                ["Timestamp (UTC)", "User", "Action", "Resource Type", "Resource Id", "Succeeded", "Error"],
                audits.Select(a => (IReadOnlyList<string?>)[a.TimestampUtc.ToString("u"), a.UserEmail, a.Action, a.ResourceType, a.ResourceId?.ToString(), a.Succeeded.ToString(), a.ErrorMessage]).ToList()
            );
        }
    }
}
