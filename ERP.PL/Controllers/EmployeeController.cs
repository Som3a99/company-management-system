using ERP.BLL.Interfaces;
using ERP.DAL.Models;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PL.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly IEmployeeRepository _employeeRepository;

        public EmployeeController(IEmployeeRepository employeeRepository)
        {
            _employeeRepository=employeeRepository;
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
    }
}
