using AutoMapper;
using ERP.PL.ViewModels.Employee;

namespace ERP.PL.Mapping.Employee
{
    public class EmployeeProfile : Profile
    {
        public EmployeeProfile()
        {
            CreateMap<DAL.Models.Employee, EmployeeViewModel>().ReverseMap();
        }
    }
}
