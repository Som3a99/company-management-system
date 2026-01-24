using AutoMapper;
using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Models;
using ERP.PL.Helpers;
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
        public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 10, string? searchTerm = null)
        {
            try
            {
                // ✅ Sanitize search input
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = InputSanitizer.NormalizeWhitespace(searchTerm);
                }

                ViewData["SearchTerm"] = searchTerm;

                PagedResult<Department> pagedDepartments;

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var searchLower = searchTerm.ToLower();
                    pagedDepartments = await _unitOfWork.DepartmentRepository.GetPagedAsync(
                        pageNumber,
                        pageSize,
                        filter: d =>
                            d.DepartmentCode.ToLower().Contains(searchLower) ||
                            d.DepartmentName.ToLower().Contains(searchLower) ||
                            (d.Manager != null && (
                                d.Manager.FirstName.ToLower().Contains(searchLower) ||
                                d.Manager.LastName.ToLower().Contains(searchLower)
                            )),
                        orderBy: q => q.OrderBy(d => d.DepartmentCode)
                    );
                }
                else
                {
                    pagedDepartments = await _unitOfWork.DepartmentRepository.GetPagedAsync(
                        pageNumber,
                        pageSize,
                        orderBy: q => q.OrderBy(d => d.DepartmentCode)
                    );
                }

                var departmentViewModels = _mapper.Map<List<DepartmentViewModel>>(pagedDepartments.Items);

                var pagedResult = new PagedResult<DepartmentViewModel>(
                    departmentViewModels,
                    pagedDepartments.TotalCount,
                    pagedDepartments.PageNumber,
                    pagedDepartments.PageSize
                );

                return View(pagedResult);
            }
            catch (Exception)
            {
                // Log error
                return View(new PagedResult<DepartmentViewModel>(new List<DepartmentViewModel>(), 0, 1, pageSize));
            }
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

            if (!string.IsNullOrWhiteSpace(department.DepartmentCode))
            {
                department.DepartmentCode = InputSanitizer.SanitizeDepartmentCode(department.DepartmentCode)
                    ?? department.DepartmentCode;
            }

            if (!string.IsNullOrWhiteSpace(department.DepartmentName))
            {
                department.DepartmentName = InputSanitizer.NormalizeWhitespace(department.DepartmentName);
            }

            if (ModelState.IsValid)
            {
                // Validate manager logic
                var (isValid, errorMessage) = await ValidateManagerAssignmentAsync(
                    department.ManagerId,
                    null  // null for new departments
                );

                if (!isValid)
                {
                    ModelState.AddModelError("ManagerId", errorMessage ?? "Invalid manager selection");
                    await LoadManagersAsync(department.ManagerId);
                    return View(department);
                }

                var mappedDepartment = _mapper.Map<Department>(department);
                await _unitOfWork.DepartmentRepository.AddAsync(mappedDepartment);
                await _unitOfWork.CompleteAsync();

                TempData["SuccessMessage"] = $"Department '{mappedDepartment.DepartmentName}' created successfully!";
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
                // Validate manager logic with department ID
                var (isValid, errorMessage) = await ValidateManagerAssignmentAsync(
                    department.ManagerId,
                    department.Id  // Pass existing department ID
                );

                if (!isValid)
                {
                    ModelState.AddModelError("ManagerId", errorMessage ?? "Invalid manager selection");
                    await LoadManagersAsync(department.ManagerId, department.Id);
                    return View(department);
                }

                var existingDepartment =
                    await _unitOfWork.DepartmentRepository.GetByIdAsync(department.Id);

                if (existingDepartment == null)
                    return NotFound();

                _mapper.Map(department, existingDepartment);
                _unitOfWork.DepartmentRepository.Update(existingDepartment);
                await _unitOfWork.CompleteAsync();

                TempData["SuccessMessage"] = $"Department '{existingDepartment.DepartmentName}' updated successfully!";
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

            TempData["SuccessMessage"] = $"Department '{department.DepartmentName}' deleted successfully!";
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

        #region Remote Validation

        /// <summary>
        /// Remote validation for department code uniqueness
        /// Called by jQuery Unobtrusive Validation on client-side
        /// </summary>
        [AcceptVerbs("GET", "POST")]
        public async Task<IActionResult> IsDepartmentCodeUnique(string departmentCode, int? id)
        {
            if (string.IsNullOrWhiteSpace(departmentCode))
                return Json(true); // Required validation will catch this

            // Sanitize input
            var sanitizedCode = InputSanitizer.SanitizeDepartmentCode(departmentCode);
            if (sanitizedCode == null)
                return Json(false); // Invalid format

            var exists = await _unitOfWork.DepartmentRepository.DepartmentCodeExistsAsync(sanitizedCode, id);
            return Json(!exists);
        }
        #endregion

        #region Helper Methods

        /// <summary>
        /// Loads available managers for department assignment dropdown
        /// </summary>
        /// <param name="currentManagerId">Currently selected manager ID</param>
        /// <param name="currentDepartmentId">Current department ID (null for new departments)</param>
        private async Task LoadManagersAsync(int? currentManagerId = null, int? currentDepartmentId = null)
        {
            var employees = await _unitOfWork.EmployeeRepository.GetAllAsync();
            var departments = await _unitOfWork.DepartmentRepository.GetAllAsync();

            // Get all employees who are already managing a department (except current)
            var managingEmployeeIds = departments
                .Where(d => d.ManagerId.HasValue && d.Id != currentDepartmentId)
                .Select(d => d.ManagerId!.Value)
                .ToHashSet();

            // Filter available managers: active, not deleted, not managing another department
            var availableManagers = employees
                .Where(e => e.IsActive &&
                           !e.IsDeleted &&
                           !managingEmployeeIds.Contains(e.Id))
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

        /// <summary>
        /// Validates manager assignment with proper null handling
        /// </summary>
        /// <param name="managerId">Manager employee ID to validate</param>
        /// <param name="currentDepartmentId">Current department ID (null for new departments)</param>
        /// <returns>Tuple with validation result and error message</returns>
        private async Task<(bool IsValid, string? ErrorMessage)> ValidateManagerAssignmentAsync(
            int? managerId,
            int? currentDepartmentId)
        {
            // No manager selected is valid (optional field)
            if (!managerId.HasValue)
                return (true, null);

            // Verify manager exists and is active
            var manager = await _unitOfWork.EmployeeRepository.GetByIdAsync(managerId.Value);
            if (manager == null)
                return (false, "Selected manager does not exist.");

            if (!manager.IsActive)
                return (false, $"{manager.FirstName} {manager.LastName} is not an active employee.");

            if (manager.IsDeleted)
                return (false, $"{manager.FirstName} {manager.LastName} is no longer available.");

            // Check if manager already manages another department
            var existingManagedDepartment = await _unitOfWork.DepartmentRepository
                .GetByManagerIdAsync(managerId.Value, currentDepartmentId);

            if (existingManagedDepartment != null)
            {
                return (false,
                    $"{manager.FirstName} {manager.LastName} is already managing '{existingManagedDepartment.DepartmentName}'. " +
                    $"An employee can only manage one department at a time.");
            }

            // Only validate department match for EXISTING departments
            // For NEW departments (currentDepartmentId == null), skip this check
            if (currentDepartmentId.HasValue)
            {
                if (manager.DepartmentId != currentDepartmentId.Value)
                {
                    var currentDept = await _unitOfWork.DepartmentRepository.GetByIdAsync(currentDepartmentId.Value);
                    return (false,
                        $"{manager.FirstName} {manager.LastName} belongs to '{manager.Department?.DepartmentName}' " +
                        $"but must belong to '{currentDept?.DepartmentName}' to manage it.");
                }
            }
            // For NEW departments: Manager can be from any department initially
            // After creation, they should ideally be transferred to the department they manage

            return (true, null);
        }

        #endregion
    }
}

