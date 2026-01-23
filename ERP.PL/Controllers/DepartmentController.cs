using AutoMapper;
using ERP.BLL.Interfaces;
using ERP.DAL.Models;
using ERP.PL.ViewModels.Department;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

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

        #region Index
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var departments = await _unitOfWork.DepartmentRepository.GetAllAsync();
            var departmentViewModels = _mapper.Map<IEnumerable<DepartmentViewModel>>(departments);
            return View(departmentViewModels);
        }
        #endregion

        #region Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadManagersAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DepartmentViewModel department)
        {
            ModelState.Remove("Manager"); // Remove Manager from ModelState validation it's not bound from the form
            if (ModelState.IsValid)
            {
                // Validate manager logic
                var (ok, err) = await ValidateManagerAssignmentAsync(department.ManagerId, department.Id);
                if (!ok)
                {
                    ModelState.AddModelError("ManagerId", err ?? "");
                    await LoadManagersAsync(department.ManagerId);
                    return View(department);
                }

                var mappedDepartment = _mapper.Map<Department>(department);
                await _unitOfWork.DepartmentRepository.AddAsync(mappedDepartment);
                await _unitOfWork.CompleteAsync();
                return RedirectToAction(nameof(Index));
            }
            await LoadManagersAsync();
            return View(department);
        }
        #endregion

        #region Edit
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var department = await _unitOfWork.DepartmentRepository.GetByIdAsync(id);
            if (department == null)
            {
                return NotFound();
            }
            var departmentViewModel = _mapper.Map<DepartmentViewModel>(department);
            await LoadManagersAsync(departmentViewModel.ManagerId);
            return View(departmentViewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(DepartmentViewModel department)
        {
            ModelState.Remove("Manager"); // Remove Manager from ModelState validation it's not bound from the form
            if (ModelState.IsValid)
            {
                // Validate manager logic
                var (ok, err) = await ValidateManagerAssignmentAsync(department.ManagerId, department.Id);
                if (!ok)
                {
                    ModelState.AddModelError("ManagerId", err ?? "");
                    await LoadManagersAsync(department.ManagerId);
                    return View(department);
                }

                var existingDepartment =
                    await _unitOfWork.DepartmentRepository.GetByIdAsync(department.Id);

                if (existingDepartment == null)
                    return NotFound();

                _mapper.Map(department, existingDepartment);
                _unitOfWork.DepartmentRepository.Update(existingDepartment);
                await _unitOfWork.CompleteAsync();
                return RedirectToAction(nameof(Index));
            }
            await LoadManagersAsync(department.ManagerId);
            return View(department);

        }
        #endregion

        #region Delete
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var department = await _unitOfWork.DepartmentRepository.GetByIdAsync(id);

            if (department == null)
                return NotFound();
            var departmentViewModel = _mapper.Map<DepartmentViewModel>(department);
            return View(departmentViewModel);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var department = await _unitOfWork.DepartmentRepository.GetByIdAsync(id);

            if (department == null)
                return NotFound();

            // Check if department has employees
            if (department.Employees.Any(e => !e.IsDeleted))
            {
                TempData["ErrorMessage"] = $"Cannot delete {department.DepartmentName} because it has active employees.";
                return RedirectToAction(nameof(Index));
            }

            _unitOfWork.DepartmentRepository.Delete(id);
            await _unitOfWork.CompleteAsync();
            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region DepartmentEmployees
        // New action to view all employees in a specific department
        [HttpGet]
        public async Task<IActionResult> DepartmentEmployees(int id)
        {
            var department = await _unitOfWork.DepartmentRepository.GetByIdAsync(id);

            if (department == null)
                return NotFound();

            var departmentViewModel = _mapper.Map<DepartmentViewModel>(department);

            return View(departmentViewModel);
        }
        #endregion

        #region Helper Methods

        // Load eligible managers (active employees only)
        private async Task LoadManagersAsync(int? currentManagerId = null)
        {
            var employees = await _unitOfWork.EmployeeRepository.GetAllAsync();

            // Get all employees who are NOT already managing a department
            var departments = await _unitOfWork.DepartmentRepository.GetAllAsync();
            var managingEmployeeIds = departments
                .Where(d => d.ManagerId.HasValue && d.ManagerId != currentManagerId)
                .Select(d => d.ManagerId!.Value)
                .ToHashSet();

            var availableManagers = employees
                .Where(e => e.IsActive && !e.IsDeleted && !managingEmployeeIds.Contains(e.Id))
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName);

            ViewBag.Managers = new SelectList(
                availableManagers.Select(e => new
                {
                    e.Id,
                    DisplayText = $"{e.FirstName} {e.LastName} - {e.Position}"
                }),
                "Id",
                "DisplayText",
                currentManagerId
            );
        }

        private async Task<(bool IsValid, string? ErrorMessage)> ValidateManagerAssignmentAsync(int? managerId, int? currentDepartmentId)
        {
            if (!managerId.HasValue) return (true, null);

            var manager = await _unitOfWork.EmployeeRepository.GetByIdAsync(managerId.Value);
            if (manager == null)
                return (false, "Selected manager does not exist.");

            // check if manager already manages another department
            var existing = (await _unitOfWork.DepartmentRepository.GetAllAsync())
                .FirstOrDefault(d => d.ManagerId == managerId && d.Id != currentDepartmentId);

            if (existing != null)
                return (false, $"{manager.FirstName} {manager.LastName} is already managing {existing.DepartmentName}.");

            if (manager.DepartmentId != currentDepartmentId)
                return (false, "Manager must belong to the same department.");

            return (true, null);
        }


        #endregion
    }
}
