using AutoMapper;
using ERP.PL.ViewModels.Project;

namespace ERP.PL.Mapping.Project
{
    public class ProjectProfile : Profile
    {
        public ProjectProfile()
        {
            // Map from Entity to ViewModel (for display)
            CreateMap<DAL.Models.Project, ProjectViewModel>();

            // Map from ViewModel to Entity (for Create/Update)
            CreateMap<ProjectViewModel, DAL.Models.Project>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Department, opt => opt.Ignore())
                .ForMember(dest => dest.ProjectManager, opt => opt.Ignore())
                .ForMember(dest => dest.Employees, opt => opt.Ignore());

            // Map from Entity to ProjectProfileViewModel
            CreateMap<DAL.Models.Project, ProjectProfileViewModel>()
                // Basic info maps automatically

                // Department info
                .ForMember(dest => dest.DepartmentCode,
                    opt => opt.MapFrom(src => src.Department != null ? src.Department.DepartmentCode : null))
                .ForMember(dest => dest.DepartmentName,
                    opt => opt.MapFrom(src => src.Department != null ? src.Department.DepartmentName : null))

                // Project Manager info
                .ForMember(dest => dest.ProjectManagerId,
                    opt => opt.MapFrom(src => src.ProjectManager != null ? src.ProjectManager.Id : (int?)null))
                .ForMember(dest => dest.ManagerFirstName,
                    opt => opt.MapFrom(src => src.ProjectManager != null ? src.ProjectManager.FirstName : null))
                .ForMember(dest => dest.ManagerLastName,
                    opt => opt.MapFrom(src => src.ProjectManager != null ? src.ProjectManager.LastName : null))
                .ForMember(dest => dest.ManagerPosition,
                    opt => opt.MapFrom(src => src.ProjectManager != null ? src.ProjectManager.Position : null))
                .ForMember(dest => dest.ManagerEmail,
                    opt => opt.MapFrom(src => src.ProjectManager != null ? src.ProjectManager.Email : null))
                .ForMember(dest => dest.ManagerImageUrl,
                    opt => opt.MapFrom(src => src.ProjectManager != null ? src.ProjectManager.ImageUrl : null))

                // Statistics — use ProjectEmployees junction table (not the legacy
                // Employees direct-FK collection) so team counts are accurate.
                .ForMember(dest => dest.TotalTeamMembers,
                    opt => opt.MapFrom(src => src.ProjectEmployees.Count(pe => !pe.Employee.IsDeleted)))
                .ForMember(dest => dest.ActiveTeamMembers,
                    opt => opt.MapFrom(src => src.ProjectEmployees.Count(pe => !pe.Employee.IsDeleted && pe.Employee.IsActive)))
                .ForMember(dest => dest.InactiveTeamMembers,
                    opt => opt.MapFrom(src => src.ProjectEmployees.Count(pe => !pe.Employee.IsDeleted && !pe.Employee.IsActive)))
                .ForMember(dest => dest.AverageTeamSalary,
                    opt => opt.MapFrom(src => src.ProjectEmployees.Any(pe => !pe.Employee.IsDeleted)
                        ? src.ProjectEmployees.Where(pe => !pe.Employee.IsDeleted).Average(pe => pe.Employee.Salary)
                        : 0))
                .ForMember(dest => dest.TotalTeamSalaryExpense,
                    opt => opt.MapFrom(src => src.ProjectEmployees.Any(pe => !pe.Employee.IsDeleted)
                        ? src.ProjectEmployees.Where(pe => !pe.Employee.IsDeleted).Sum(pe => pe.Employee.Salary)
                        : 0))
                .ForMember(dest => dest.DaysInProgress,
                    opt => opt.MapFrom(src => (DateTime.Now - src.StartDate).Days))
                .ForMember(dest => dest.DaysRemaining,
                    opt => opt.MapFrom(src => src.EndDate.HasValue
                        ? (src.EndDate.Value - DateTime.Now).Days > 0
                            ? (int?)(src.EndDate.Value - DateTime.Now).Days
                            : 0
                        : null))
                .ForMember(dest => dest.BudgetPerTeamMember,
                    opt => opt.MapFrom(src => src.ProjectEmployees.Any(pe => !pe.Employee.IsDeleted)
                        ? src.Budget / src.ProjectEmployees.Count(pe => !pe.Employee.IsDeleted)
                        : src.Budget))

                // Team Members (top 10, ordered by name) — sourced from junction table
                .ForMember(dest => dest.TeamMembers,
                    opt => opt.MapFrom(src => src.ProjectEmployees
                        .Where(pe => !pe.Employee.IsDeleted)
                        .OrderBy(pe => pe.Employee.LastName)
                        .ThenBy(pe => pe.Employee.FirstName)
                        .Take(10)
                        .Select(pe => new ProjectProfileViewModel.TeamMemberSummary
                        {
                            Id = pe.Employee.Id,
                            FirstName = pe.Employee.FirstName,
                            LastName = pe.Employee.LastName,
                            Position = pe.Employee.Position,
                            Email = pe.Employee.Email,
                            ImageUrl = pe.Employee.ImageUrl,
                            IsActive = pe.Employee.IsActive,
                            DepartmentId = pe.Employee.DepartmentId,
                            DepartmentName = pe.Employee.Department != null ? pe.Employee.Department.DepartmentName : null
                        })
                        .ToList()));
        }
    }
}
