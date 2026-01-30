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

        }
    }
}
