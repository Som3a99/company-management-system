using AutoMapper;
using ERP.DAL.Models;
using ERP.PL.ViewModels.Department;

namespace ERP.PL.Mapping.Department
{
    public class DepartmentProfile : Profile
    {
        public DepartmentProfile()
        {
            // Map from Entity to ViewModel (for display)
            CreateMap<DAL.Models.Department, DepartmentViewModel>()
               .ForMember(dest => dest.Projects, opt => opt.MapFrom(src => src.Projects));
;

            // Map from ViewModel to Entity (for Create/Update)
            CreateMap<DepartmentViewModel, DAL.Models.Department>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Employees, opt => opt.Ignore())
                .ForMember(dest => dest.Manager, opt => opt.Ignore())
                .ForMember(dest => dest.Projects, opt => opt.Ignore());

            // Map Department → DepartmentProfileViewModel
            CreateMap<DAL.Models.Department, DepartmentProfileViewModel>()
                // Basic info maps automatically (Id, DepartmentCode, DepartmentName, CreatedAt)

                // Manager Info
                .ForMember(dest => dest.ManagerId,
                    opt => opt.MapFrom(src => src.Manager != null ? src.Manager.Id : (int?)null))
                .ForMember(dest => dest.ManagerName,
                    opt => opt.MapFrom(src => src.Manager != null
                        ? $"{src.Manager.FirstName} {src.Manager.LastName}"
                        : null))
                .ForMember(dest => dest.ManagerPosition,
                    opt => opt.MapFrom(src => src.Manager != null ? src.Manager.Position : null))
                .ForMember(dest => dest.ManagerEmail,
                    opt => opt.MapFrom(src => src.Manager != null ? src.Manager.Email : null))
                .ForMember(dest => dest.ManagerImageUrl,
                    opt => opt.MapFrom(src => src.Manager != null ? src.Manager.ImageUrl : null))

                // Statistics - Employee counts
                .ForMember(dest => dest.TotalEmployees,
                    opt => opt.MapFrom(src => src.Employees.Count(e => !e.IsDeleted)))
                .ForMember(dest => dest.ActiveEmployees,
                    opt => opt.MapFrom(src => src.Employees.Count(e => !e.IsDeleted && e.IsActive)))
                .ForMember(dest => dest.InactiveEmployees,
                    opt => opt.MapFrom(src => src.Employees.Count(e => !e.IsDeleted && !e.IsActive)))

                // Statistics - Project counts
                .ForMember(dest => dest.TotalProjects,
                    opt => opt.MapFrom(src => src.Projects.Count(p => !p.IsDeleted)))
                .ForMember(dest => dest.ActiveProjects,
                    opt => opt.MapFrom(src => src.Projects.Count(p => !p.IsDeleted &&
                        (p.Status == ProjectStatus.Planning || p.Status == ProjectStatus.InProgress))))
                .ForMember(dest => dest.CompletedProjects,
                    opt => opt.MapFrom(src => src.Projects.Count(p => !p.IsDeleted &&
                        p.Status == ProjectStatus.Completed)))

                // Statistics - Financial
                .ForMember(dest => dest.TotalBudget,
                    opt => opt.MapFrom(src => src.Projects.Any(p => !p.IsDeleted)
                        ? src.Projects.Where(p => !p.IsDeleted).Sum(p => p.Budget)
                        : 0m))
                .ForMember(dest => dest.AverageSalary,
                    opt => opt.MapFrom(src => src.Employees.Any(e => !e.IsDeleted)
                        ? src.Employees.Where(e => !e.IsDeleted).Average(e => e.Salary)
                        : 0m))

                // Recent Employees (top 10, ordered by name)
                .ForMember(dest => dest.RecentEmployees,
                    opt => opt.MapFrom(src => src.Employees
                        .Where(e => !e.IsDeleted)
                        .OrderBy(e => e.LastName)
                        .ThenBy(e => e.FirstName)
                        .Take(10)
                        .Select(e => new DepartmentProfileViewModel.EmployeeSummary
                        {
                            Id = e.Id,
                            Name = $"{e.FirstName} {e.LastName}",
                            Position = e.Position,
                            ImageUrl = e.ImageUrl,
                            IsActive = e.IsActive,
                            ProjectId = e.ProjectId,
                            ProjectName = e.Project != null ? e.Project.ProjectName : null
                        })
                        .ToList()))

                // Recent Projects (top 5, prioritizing In Progress, then by start date)
                .ForMember(dest => dest.RecentProjects,
                    opt => opt.MapFrom(src => src.Projects
                        .Where(p => !p.IsDeleted)
                        .OrderByDescending(p => p.Status == ProjectStatus.InProgress)
                        .ThenByDescending(p => p.Status == ProjectStatus.Planning)
                        .ThenBy(p => p.StartDate)
                        .Take(5)
                        .Select(p => new DepartmentProfileViewModel.ProjectSummary
                        {
                            Id = p.Id,
                            ProjectCode = p.ProjectCode,
                            ProjectName = p.ProjectName,
                            Status = p.Status,
                            ManagerId = p.ProjectManagerId,
                            ManagerName = p.ProjectManager != null
                                ? $"{p.ProjectManager.FirstName} {p.ProjectManager.LastName}"
                                : null,
                            TeamSize = p.Employees.Count(e => !e.IsDeleted),
                            Budget = p.Budget
                        })
                        .ToList()));
        }
    }
}
