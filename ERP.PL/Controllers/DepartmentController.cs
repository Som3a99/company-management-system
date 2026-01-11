using AutoMapper;
using ERP.BLL.Interfaces;
using ERP.DAL.Models;
using ERP.PL.ViewModels.Department;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PL.Controllers
{
    public class DepartmentController : Controller
    {
        private readonly IDepartmentRepository _departmentRepository;
        private readonly IMapper _mapper;

        public DepartmentController(IDepartmentRepository departmentRepository, IMapper mapper)
        {
            _departmentRepository = departmentRepository;
            _mapper=mapper;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var departments = _departmentRepository.GetAll();
            var departmentViewModels = _mapper.Map<IEnumerable<DepartmentViewModel>>(departments);
            return View(departmentViewModels);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(DepartmentViewModel department)
        {
            if (ModelState.IsValid)
            {
                var mappedDepartment = _mapper.Map<Department>(department);
                _departmentRepository.Add(mappedDepartment);
                return RedirectToAction("Index");
            }
            return View(department);
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var department = _departmentRepository.GetById(id);
            if (department == null)
            {
                return NotFound();
            }
            var departmentViewModel = _mapper.Map<DepartmentViewModel>(department);
            return View(departmentViewModel);
        }

        [HttpPost]

        public IActionResult Edit(DepartmentViewModel department)
        {
            if (ModelState.IsValid)
            {
                var mappedDepartment = _mapper.Map<Department>(department);
                _departmentRepository.Update(mappedDepartment);
                return RedirectToAction("Index");
            }
            return View(department);

        }

        [HttpGet]
        public IActionResult Delete(int id)
        {
            var department = _departmentRepository.GetById(id);

            if (department == null)
                return NotFound();
            var departmentViewModel = _mapper.Map<DepartmentViewModel>(department);
            return View(departmentViewModel);
        }

        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            var department = _departmentRepository.GetById(id);

            if (department == null)
                return NotFound();

            _departmentRepository.Delete(id);
            return RedirectToAction(nameof(Index));
        }

        // New action to view all employees in a specific department
        [HttpGet]
        public IActionResult DepartmentEmployees(int id)
        {
            var department = _departmentRepository.GetById(id);

            if (department == null)
                return NotFound();

            var departmentViewModel = _mapper.Map<DepartmentViewModel>(department);

            return View(departmentViewModel);
        }
    }
}
