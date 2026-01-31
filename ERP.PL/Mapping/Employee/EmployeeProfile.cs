using AutoMapper;
using ERP.PL.ViewModels.Employee;

namespace ERP.PL.Mapping.Employee
{
    public class EmployeeProfile : Profile
    {
        public EmployeeProfile()
        {
            // Map from Entity to ViewModel (for display)
            CreateMap<DAL.Models.Employee, EmployeeViewModel>();

            // Map from ViewModel to Entity (for updates)
            CreateMap<EmployeeViewModel, DAL.Models.Employee>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ImageUrl, opt => opt.Ignore())
                .ForMember(dest => dest.Department, opt => opt.Ignore())
                .ForMember(dest => dest.ManagedDepartment, opt => opt.Ignore())
                .ForMember(dest => dest.Project, opt => opt.Ignore());

            // Map from Entity to EmployeeProfileViewModel
            CreateMap<DAL.Models.Employee, EmployeeProfileViewModel>()
                // Basic info maps automatically

                // Department info
                .ForMember(dest => dest.DepartmentName,
                    opt => opt.MapFrom(src => src.Department != null ? src.Department.DepartmentName : null))
                .ForMember(dest => dest.DepartmentCode,
                    opt => opt.MapFrom(src => src.Department != null ? src.Department.DepartmentCode : null))

                // Managed Department
                .ForMember(dest => dest.ManagedDepartmentId,
                    opt => opt.MapFrom(src => src.ManagedDepartment != null ? src.ManagedDepartment.Id : (int?)null))
                .ForMember(dest => dest.ManagedDepartmentName,
                    opt => opt.MapFrom(src => src.ManagedDepartment != null ? src.ManagedDepartment.DepartmentName : null))
                .ForMember(dest => dest.ManagedDepartmentCode,
                    opt => opt.MapFrom(src => src.ManagedDepartment != null ? src.ManagedDepartment.DepartmentCode : null))
                .ForMember(dest => dest.ManagedDepartmentEmployeeCount,
                    opt => opt.MapFrom(src => src.ManagedDepartment != null
                        ? src.ManagedDepartment.Employees.Count(e => !e.IsDeleted)
                        : 0))

                // Managed Project
                .ForMember(dest => dest.ManagedProjectId,
                    opt => opt.MapFrom(src => src.ManagedProject != null ? src.ManagedProject.Id : (int?)null))
                .ForMember(dest => dest.ManagedProjectName,
                    opt => opt.MapFrom(src => src.ManagedProject != null ? src.ManagedProject.ProjectName : null))
                .ForMember(dest => dest.ManagedProjectCode,
                    opt => opt.MapFrom(src => src.ManagedProject != null ? src.ManagedProject.ProjectCode : null))
                .ForMember(dest => dest.ManagedProjectDepartmentId,
                    opt => opt.MapFrom(src => src.ManagedProject != null ? src.ManagedProject.DepartmentId : (int?)null))
                .ForMember(dest => dest.ManagedProjectDepartmentName,
                    opt => opt.MapFrom(src => src.ManagedProject != null && src.ManagedProject.Department != null
                        ? src.ManagedProject.Department.DepartmentName
                        : null))

                // Assigned Project
                .ForMember(dest => dest.AssignedProjectId,
                    opt => opt.MapFrom(src => src.Project != null ? src.Project.Id : (int?)null))
                .ForMember(dest => dest.AssignedProjectName,
                    opt => opt.MapFrom(src => src.Project != null ? src.Project.ProjectName : null))
                .ForMember(dest => dest.AssignedProjectCode,
                    opt => opt.MapFrom(src => src.Project != null ? src.Project.ProjectCode : null))
                .ForMember(dest => dest.AssignedProjectManagerId,
                    opt => opt.MapFrom(src => src.Project != null ? src.Project.ProjectManagerId : (int?)null))
                .ForMember(dest => dest.AssignedProjectManagerName,
                    opt => opt.MapFrom(src => src.Project != null && src.Project.ProjectManager != null
                        ? $"{src.Project.ProjectManager.FirstName} {src.Project.ProjectManager.LastName}"
                        : null))
                .ForMember(dest => dest.AssignedProjectDepartmentId,
                    opt => opt.MapFrom(src => src.Project != null ? src.Project.DepartmentId : (int?)null))
                .ForMember(dest => dest.AssignedProjectDepartmentName,
                    opt => opt.MapFrom(src => src.Project != null && src.Project.Department != null
                        ? src.Project.Department.DepartmentName
                        : null));
        }
    }
}
