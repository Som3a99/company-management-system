using ERP.BLL.Interfaces;
using ERP.DAL.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ERP.PL.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IDepartmentRepository _departmentRepository;


        public EmployeeController(IEmployeeRepository employeeRepository, IDepartmentRepository departmentRepository)
        {
            _employeeRepository=employeeRepository;
            _departmentRepository=departmentRepository;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var employees = _employeeRepository.GetAll();
            return View(employees);
        }

        [HttpGet]
        public IActionResult Create()
        {
            LoadDepartments();
            return View();
        }

        [HttpPost]
        public IActionResult Create(Employee employee)
        {
            if (ModelState.IsValid)
            {
                _employeeRepository.Add(employee);
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
            LoadDepartments();
            return View(employee);
        }

        [HttpPost]
        public IActionResult Edit(Employee employee)
        {
            if (ModelState.IsValid)
            {
                _employeeRepository.Update(employee);
                return RedirectToAction("Index");
            }
            LoadDepartments();
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
            return View(employee);
        }

        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            _employeeRepository.Delete(id);
            return RedirectToAction("Index");
        }

        // Helper method to load departments for dropdown
        private void LoadDepartments()
        {
            var departments = _departmentRepository.GetAll();
            ViewBag.Departments = new SelectList(
                departments.Select(d => new
                {
                    Id = d.Id,
                    DisplayText = $"{d.DepartmentCode} - {d.DepartmentName}"
                }),
                "Id",
                "DisplayText"
            );
        }
    }
}
