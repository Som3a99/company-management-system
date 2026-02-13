namespace ERP.BLL.Reporting.Dtos;

public sealed class ReportRequestDto
{
    public DateTime? StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public int? DepartmentId { get; set; }
    public int? ProjectId { get; set; }
}

public sealed class TaskReportRowDto
{
    public int TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? Project { get; set; }
    public string? Assignee { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal ActualHours { get; set; }
}

public sealed class ProjectReportRowDto
{
    public int ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
}

public sealed class DepartmentReportRowDto
{
    public int DepartmentId { get; set; }
    public string DepartmentCode { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public int EmployeesCount { get; set; }
    public int ProjectsCount { get; set; }
    public int OpenTasksCount { get; set; }
}

public sealed class AuditReportRowDto
{
    public DateTime TimestampUtc { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? ResourceType { get; set; }
    public int? ResourceId { get; set; }
    public bool Succeeded { get; set; }
    public string? ErrorMessage { get; set; }
}