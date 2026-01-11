using AutoMapper;
using ERP.BLL.Interfaces;
using ERP.DAL.Models;
using ERP.PL.ViewModels.Employee;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ERP.PL.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IDepartmentRepository _departmentRepository;
        private readonly IMapper _mapper;


        public EmployeeController(IEmployeeRepository employeeRepository, IDepartmentRepository departmentRepository, IMapper mapper)
        {
            _employeeRepository=employeeRepository;
            _departmentRepository=departmentRepository;
            _mapper=mapper;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var employees = _employeeRepository.GetAll();
            var employeeViewModels = _mapper.Map<IEnumerable<EmployeeViewModel>>(employees);
            return View(employeeViewModels);
        }

        [HttpGet]
        public IActionResult Create()
        {
            LoadDepartments();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(EmployeeViewModel employee)
        {
            if (ModelState.IsValid)
            {
                var mappedEmployee = _mapper.Map<Employee>(employee);
                _employeeRepository.Add(mappedEmployee);
                return RedirectToAction("Index");
            }
            LoadDepartments();
            return View(employee);
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var employee = _employeeRepository.GetById(id);
            if (employee == null)
            {
                return NotFound();
            }
            LoadDepartments(employee.DepartmentId);

            var employeeViewModel = _mapper.Map<EmployeeViewModel>(employee);
            return View(employeeViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(EmployeeViewModel employee)
        {
            if (ModelState.IsValid)
            {
                var mappedEmployee = _mapper.Map<Employee>(employee);
                _employeeRepository.Update(mappedEmployee);
                return RedirectToAction("Index");
            }
            LoadDepartments(employee.DepartmentId);
            return View(employee);
        }

        [HttpGet]
        public IActionResult Delete(int id)
        {
            var employee = _employeeRepository.GetById(id);
            if (employee == null)
            {
                return NotFound();
            }
            var employeeViewModel = _mapper.Map<EmployeeViewModel>(employee);
            return View(employeeViewModel);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            _employeeRepository.Delete(id);
            return RedirectToAction("Index");
        }

        // Helper method to load departments for dropdown
        private void LoadDepartments(int? selectedDepartmentId = null)
        {
            var departments = _departmentRepository.GetAll();
            ViewBag.Departments = new SelectList(
                departments.Select(d => new
                {
                    Id = d.Id,
                    DisplayText = $"{d.DepartmentCode} - {d.DepartmentName}"
                }),
                "Id",
                "DisplayText",
                selectedDepartmentId
            );
        }
    }
}
