using AutoMapper;
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
        }
    }
}
