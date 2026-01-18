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
                .ForMember(dest => dest.ImageUrl, opt => opt.Ignore())
                .ForMember(dest => dest.Department, opt => opt.Ignore());
        }
    }
}
