using ERP.BLL.Interfaces;
using ERP.DAL.Models;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PL.Controllers
{
    public class DepartmentController : Controller
    {
        private readonly IDepartmentRepository _departmentRepository;

        public DepartmentController(IDepartmentRepository departmentRepository)
        {
            _departmentRepository = departmentRepository;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var departments = _departmentRepository.GetAll();
            return View(departments);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(Department department)
        {
            if (ModelState.IsValid)
            {
                _departmentRepository.Add(department);
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
            return View(department);
        }

        [HttpPost]

        public IActionResult Edit(Department department)
        {
            if (ModelState.IsValid)
            {
                _departmentRepository.Update(department);
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

            return View(department);
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

            return View(department);
        }


    }
}
