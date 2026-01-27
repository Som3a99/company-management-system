using AutoMapper;
using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using ERP.DAL.Models;
using ERP.PL.Helpers;
using ERP.PL.ViewModels.Department;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

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

                ViewData["SearchTerm"] = searchTerm;

                string? likePattern = null;

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = InputSanitizer.NormalizeWhitespace(searchTerm);
                    searchTerm = InputSanitizer.SanitizeLikeQuery(searchTerm);
                    likePattern = $"%{searchTerm}%";
                }

                var pagedDepartments = await _unitOfWork.DepartmentRepository.GetPagedAsync(
                    pageNumber,
                    pageSize,
                    filter: d =>
                        likePattern == null ||
                        EF.Functions.Like(d.DepartmentCode, likePattern) ||
                        EF.Functions.Like(d.DepartmentName, likePattern) ||
                        (d.Manager != null &&
                         (
                             EF.Functions.Like(d.Manager.FirstName, likePattern) ||
                             EF.Functions.Like(d.Manager.LastName, likePattern)
                         )),
                    orderBy: q => q.OrderBy(d => d.DepartmentCode)
                );

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
            ModelState.Remove("Manager");

            // Input sanitization
            if (!string.IsNullOrWhiteSpace(department.DepartmentCode))
            {
                department.DepartmentCode =
                    InputSanitizer.SanitizeDepartmentCode(department.DepartmentCode)
                    ?? department.DepartmentCode;
            }

            if (!string.IsNullOrWhiteSpace(department.DepartmentName))
            {
                department.DepartmentName =
                    InputSanitizer.NormalizeWhitespace(department.DepartmentName);
            }

            // Validate model first (NO transaction yet)
            if (!ModelState.IsValid)
            {
                await LoadManagersAsync(department.ManagerId);
                return View(department);
            }

            using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                //Optional UX validation (NOT trusted for safety)
                if (department.ManagerId.HasValue)
                {
                    var manager = await _unitOfWork.EmployeeRepository
                        .GetByIdAsync(department.ManagerId.Value);

                    if (manager == null)
                    {
                        ModelState.AddModelError("ManagerId", "Selected manager does not exist.");
                        await LoadManagersAsync(department.ManagerId);
                        return View(department);
                    }
                }

                // Create department
                var mappedDepartment = _mapper.Map<Department>(department);

                await _unitOfWork.DepartmentRepository.AddAsync(mappedDepartment);
                await _unitOfWork.CompleteAsync();

                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Department '{mappedDepartment.DepartmentName}' created successfully!";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
                when (ex.InnerException?.Message.Contains("IX_Departments_ManagerId_Unique") == true)
            {
                await transaction.RollbackAsync();

                // Database constraint safety net
                ModelState.AddModelError(
                    "ManagerId",
                    "This manager is already assigned to another department."
                );

                await LoadManagersAsync(department.ManagerId);
                return View(department);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();

                // Handle CHECK constraint or other DB errors
                var errorMessage = "An error occurred while creating the department.";
                if (ex.InnerException?.Message.Contains("CK_Department_DepartmentCode_Format") == true)
                {
                    errorMessage = "Invalid department code format. Expected format: ABC_123 (3 uppercase letters, underscore, 3 digits).";
                    ModelState.AddModelError("DepartmentCode", errorMessage);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, errorMessage);
                }

                await LoadManagersAsync(department.ManagerId);
                return View(department);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(DepartmentViewModel department)
        {
            // Remove navigation-only validation
            ModelState.Remove("Manager");

            // Validate model FIRST (no transaction yet)
            if (!ModelState.IsValid)
            {
                await LoadManagersAsync(department.ManagerId, department.Id);
                return View(department);
            }

            // Start transaction only when needed
            using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                // Validate manager existence & assignment (inside transaction)
                if (department.ManagerId.HasValue)
                {
                    var manager = await _unitOfWork.EmployeeRepository
                        .GetByIdAsync(department.ManagerId.Value);

                    if (manager == null)
                    {
                        ModelState.AddModelError("ManagerId", "Selected manager does not exist.");
                        await LoadManagersAsync(department.ManagerId, department.Id);
                        return View(department);
                    }

                    // Lock + check if manager already assigned elsewhere
                    var conflict = await _unitOfWork.DepartmentRepository
                        .GetDepartmentByManagerForUpdateAsync(
                            department.ManagerId.Value,
                            department.Id);

                    if (conflict != null)
                    {
                        ModelState.AddModelError(
                            "ManagerId",
                            $"Manager is already assigned to '{conflict.DepartmentName}'."
                        );

                        await LoadManagersAsync(department.ManagerId, department.Id);
                        return View(department);
                    }
                }

                // Load existing department (TRACKED for update)
                var existingDepartment =
                    await _unitOfWork.DepartmentRepository.GetByIdTrackedAsync(department.Id);

                if (existingDepartment == null)
                    return NotFound();

                // Apply allowed updates
                _mapper.Map(department, existingDepartment);
                _unitOfWork.DepartmentRepository.Update(existingDepartment);

                // Commit DB changes
                await _unitOfWork.CompleteAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Department '{existingDepartment.DepartmentName}' updated successfully!";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
                when (ex.InnerException?.Message.Contains("IX_Departments_ManagerId_Unique") == true)
            {
                await transaction.RollbackAsync();

                // DB constraint safety net
                ModelState.AddModelError(
                    "ManagerId",
                    "This manager is already assigned to another department."
                );

                await LoadManagersAsync(department.ManagerId, department.Id);
                return View(department);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();

                // Handle CHECK constraint or other DB errors
                var errorMessage = "An error occurred while saving the department.";
                if (ex.InnerException?.Message.Contains("CK_Department_DepartmentCode_Format") == true)
                {
                    errorMessage = "Invalid department code format. Expected format: ABC_123 (3 uppercase letters, underscore, 3 digits).";
                    ModelState.AddModelError("DepartmentCode", errorMessage);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, errorMessage);
                }

                await LoadManagersAsync(department.ManagerId, department.Id);
                return View(department);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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
        [ValidateAntiForgeryToken]
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

            await _unitOfWork.DepartmentRepository.DeleteAsync(id);
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

