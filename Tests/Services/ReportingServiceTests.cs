using ERP.BLL.Reporting.Dtos;
using ERP.BLL.Reporting.Services;
using ERP.DAL.Models;
using FluentAssertions;
using Tests.Infrastructure;

namespace Tests.Services
{
    public class ReportingServiceTests
    {
        [Fact]
        public async Task GetProjectReportAsync_ShouldCalculateCompletedTasks()
        {
            using var db = TestDbContextFactory.Create();
            var department = new Department { Id = 1, DepartmentCode = "DEP_001", DepartmentName = "Engineering" };
            var project = new Project
            {
                Id = 1,
                DepartmentId = 1,
                Department = department,
                ProjectCode = "PRJ_001",
                ProjectName = "ERP",
                StartDate = DateTime.UtcNow,
                Status = ProjectStatus.InProgress
            };
            db.Departments.Add(department);
            db.Projects.Add(project);
            db.TaskItems.AddRange(
                new TaskItem { Id = 1, ProjectId = 1, Project = project, Title = "A", CreatedByUserId = "u", Status = ERP.DAL.Models.TaskStatus.Completed },
                new TaskItem { Id = 2, ProjectId = 1, Project = project, Title = "B", CreatedByUserId = "u", Status = ERP.DAL.Models.TaskStatus.InProgress });
            await db.SaveChangesAsync();

            var sut = new ReportingService(db, new PassthroughCacheService());

            var rows = await sut.GetProjectReportAsync(new ReportRequestDto { DepartmentId = 1 }, scopedDepartmentId: null, scopedProjectId: null);

            rows.Should().ContainSingle();
            rows[0].TotalTasks.Should().Be(2);
            rows[0].CompletedTasks.Should().Be(1);
        }

        [Fact]
        public async Task GetTaskReportAsync_ShouldHonorScopedProject()
        {
            using var db = TestDbContextFactory.Create();
            var d = new Department { Id = 1, DepartmentCode = "DEP_001", DepartmentName = "Engineering" };
            var p1 = new Project { Id = 1, DepartmentId = 1, Department = d, ProjectCode = "PRJ_001", ProjectName = "One", StartDate = DateTime.UtcNow };
            var p2 = new Project { Id = 2, DepartmentId = 1, Department = d, ProjectCode = "PRJ_002", ProjectName = "Two", StartDate = DateTime.UtcNow };
            db.Departments.Add(d);
            db.Projects.AddRange(p1, p2);
            db.TaskItems.AddRange(
                new TaskItem { Id = 1, ProjectId = 1, Project = p1, Title = "A", CreatedByUserId = "u", CreatedAt = DateTime.UtcNow },
                new TaskItem { Id = 2, ProjectId = 2, Project = p2, Title = "B", CreatedByUserId = "u", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var sut = new ReportingService(db, new PassthroughCacheService());

            var rows = await sut.GetTaskReportAsync(new ReportRequestDto(), scopedDepartmentId: null, scopedProjectId: 1);

            rows.Should().HaveCount(1);
            rows[0].TaskId.Should().Be(1);
        }

    }
}
