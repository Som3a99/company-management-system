using AutoMapper;
using ERP.PL.ViewModels.Department;

namespace ERP.PL.Mapping.Department
{
    public class DepartmentProfile : Profile
    {
        public DepartmentProfile()
        {
            CreateMap<DAL.Models.Department, DepartmentViewModel>().ReverseMap();
        }
    }
}
