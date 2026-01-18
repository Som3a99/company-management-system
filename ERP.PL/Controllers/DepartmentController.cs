using AutoMapper;
using ERP.BLL.Interfaces;
using ERP.DAL.Models;
using ERP.PL.ViewModels.Department;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PL.Controllers
{
    public class DepartmentController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public DepartmentController(IMapper mapper, IUnitOfWork unitOfWork)
        {

            _mapper=mapper;
            _unitOfWork=unitOfWork;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var departments = _unitOfWork.DepartmentRepository.GetAll();
            var departmentViewModels = _mapper.Map<IEnumerable<DepartmentViewModel>>(departments);
            return View(departmentViewModels);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(DepartmentViewModel department)
        {
            if (ModelState.IsValid)
            {
                var mappedDepartment = _mapper.Map<Department>(department);
                _unitOfWork.DepartmentRepository.Add(mappedDepartment);
                _unitOfWork.Complete();
                return RedirectToAction(nameof(Index));
            }
            return View(department);
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var department = _unitOfWork.DepartmentRepository.GetById(id);
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
                _unitOfWork.DepartmentRepository.Update(mappedDepartment);
                _unitOfWork.Complete();
                return RedirectToAction(nameof(Index));
            }
            return View(department);

        }

        [HttpGet]
        public IActionResult Delete(int id)
        {
            var department = _unitOfWork.DepartmentRepository.GetById(id);

            if (department == null)
                return NotFound();
            var departmentViewModel = _mapper.Map<DepartmentViewModel>(department);
            return View(departmentViewModel);
        }

        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            var department = _unitOfWork.DepartmentRepository.GetById(id);

            if (department == null)
                return NotFound();

            _unitOfWork.DepartmentRepository.Delete(id);
            _unitOfWork.Complete();
            return RedirectToAction(nameof(Index));
        }

        // New action to view all employees in a specific department
        [HttpGet]
        public IActionResult DepartmentEmployees(int id)
        {
            var department = _unitOfWork.DepartmentRepository.GetById(id);

            if (department == null)
                return NotFound();

            var departmentViewModel = _mapper.Map<DepartmentViewModel>(department);

            return View(departmentViewModel);
        }
    }
}
