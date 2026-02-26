using AutoMapper;
using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Models;
using ERP.PL.Helpers;
using ERP.PL.Services;
using ERP.PL.ViewModels.Department;
using ERP.PL.ViewModels.Employee;
using ERP.PL.ViewModels.Project;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERP.PL.Controllers
{
    [Authorize]
    public class DepartmentController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<DepartmentController> _logger;
        private readonly IAuditService _auditService;
        private readonly IRoleManagementService _roleManagementService;
        private readonly ICacheService _cacheService;
        public DepartmentController(IMapper mapper, IUnitOfWork unitOfWork, ILogger<DepartmentController> logger, IAuditService auditService, IRoleManagementService roleManagementService, ICacheService cacheService)
        {

            _mapper=mapper;
            _unitOfWork=unitOfWork;
            _logger=logger;
            _auditService=auditService;
            _roleManagementService=roleManagementService;
            _cacheService=cacheService;
        }

        #region Index
        [Authorize(Policy = "RequireManager")]
        [HttpGet]
        public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 10,
                                                string? searchTerm = null, string? managerStatus = null)
        {
            try
            {
                ViewData["SearchTerm"] = searchTerm;
                ViewData["ManagerStatus"] = managerStatus ?? "all";

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
                        (likePattern == null ||
                         EF.Functions.Like(d.DepartmentCode, likePattern) ||
                         EF.Functions.Like(d.DepartmentName, likePattern) ||
                         (d.Manager != null &&
                          (EF.Functions.Like(d.Manager.FirstName, likePattern) ||
                           EF.Functions.Like(d.Manager.LastName, likePattern)))) &&
                        (managerStatus == null || managerStatus == "all" ||
                         (managerStatus == "withManager" && d.ManagerId != null) ||
                         (managerStatus == "noManager" && d.ManagerId == null)),
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
        [Authorize(Policy = "RequireManager")]
        public async Task<IActionResult> Create()
        {
            await LoadManagersAsync();
            return View();
        }

        [HttpPost]
        [Authorize(Policy = "RequireManager")]
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

            // Server-side department code uniqueness validation
            if (await _unitOfWork.DepartmentRepository.DepartmentCodeExistsAsync(department.DepartmentCode))
            {
                ModelState.AddModelError("DepartmentCode", "This department code is already in use.");
                await LoadManagersAsync(department.ManagerId);
                return View(department);
            }

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
                    // FIXED: Use Null for new departments (ID doesn't exist yet)
                    var conflict = await _unitOfWork.DepartmentRepository
                        .GetDepartmentByManagerForUpdateAsync(
                            department.ManagerId.Value,
                            null); // Null for new department

                    if (conflict != null)
                    {
                        ModelState.AddModelError(
                            "ManagerId",
                            $"Manager is already assigned to '{conflict.DepartmentName}'."
                        );

                        await LoadManagersAsync(department.ManagerId);
                        return View(department);
                    }
                }

                // Create department inside execution-strategy-safe transaction
                var mappedDepartment = _mapper.Map<Department>(department);

                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    await _unitOfWork.DepartmentRepository.AddAsync(mappedDepartment);
                    await _unitOfWork.CompleteAsync();
                });

                await InvalidateDepartmentRelatedCachesAsync(mappedDepartment.Id);

                // Audit log success (outside transaction — non-critical)
                try
                {
                    await _auditService.LogAsync(
                        User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                        User.Identity!.Name!,
                        "CREATE_DEPARTMENT_SUCCESS",
                        "Department",
                        mappedDepartment.Id,
                        details: $"Created department: {mappedDepartment.DepartmentCode} - {mappedDepartment.DepartmentName}");
                }
                catch { /* audit failure is non-critical */ }

                TempData["SuccessMessage"] =
                    $"Department '{mappedDepartment.DepartmentName}' created successfully!";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
                when (ex.InnerException?.Message.Contains("IX_Departments_ManagerId_Unique") == true)
            {
                // Audit log failure
                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "CREATE_DEPARTMENT_FAILED",
                    "Department",
                    null,
                    succeeded: false,
                    errorMessage: "Manager already assigned to another department",
                    details: $"Department code: {department.DepartmentCode}");

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
                // Audit log failure
                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "CREATE_DEPARTMENT_FAILED",
                    "Department",
                    null,
                    succeeded: false,
                    errorMessage: ex.InnerException?.Message ?? ex.Message);

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
            catch(Exception ex)
            {
                // Audit log failure
                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "CREATE_DEPARTMENT_FAILED",
                    "Department",
                    null,
                    succeeded: false,
                    errorMessage: ex.Message);

                throw;
            }
        }
        #endregion

        #region Edit
        [HttpGet]
        [Authorize(Policy = "RequireManager")]
        public async Task<IActionResult> Edit(int id)
        {
            var department = await _unitOfWork.DepartmentRepository.GetByIdAsync(id);
            if (department == null)
            {
                return NotFound();
            }
            var departmentViewModel = _mapper.Map<DepartmentViewModel>(department);
            await LoadManagersAsync(departmentViewModel.ManagerId, id);
            return View(departmentViewModel);
        }

        [HttpPost]
        [Authorize(Policy = "RequireManager")]
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
            try
            {
                // Validate manager existence & assignment
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

                    // FIXED: Add locking check for manager assignment
                    var conflict = await _unitOfWork.DepartmentRepository
                        .GetDepartmentByManagerForUpdateAsync(
                            department.ManagerId.Value,
                            department.Id); // Exclude current department

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

                // Commit DB changes inside execution-strategy-safe transaction
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    await _unitOfWork.CompleteAsync();
                });

                await InvalidateDepartmentRelatedCachesAsync(existingDepartment.Id);

                // AUTO-ASSIGN ROLE IF MANAGER CHANGED
                if (existingDepartment.ManagerId.HasValue)
                {
                    await _roleManagementService.SyncEmployeeRolesAsync(existingDepartment.ManagerId.Value);
                }

                // Audit log success (outside transaction — non-critical)
                try
                {
                    await _auditService.LogAsync(
                        User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                        User.Identity!.Name!,
                        "EDIT_DEPARTMENT_SUCCESS",
                        "Department",
                        existingDepartment.Id,
                        details: $"Updated department: {existingDepartment.DepartmentCode} - {existingDepartment.DepartmentName}");
                }
                catch { /* audit failure is non-critical */ }

                TempData["SuccessMessage"] =
                    $"Department '{existingDepartment.DepartmentName}' updated successfully!";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
                when (ex.InnerException?.Message.Contains("IX_Departments_ManagerId_Unique") == true)
            {
                // Audit log failure
                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "EDIT_DEPARTMENT_FAILED",
                    "Department",
                    department.Id,
                    succeeded: false,
                    errorMessage: "Manager already assigned to another department");


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
                // Audit log failure
                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "EDIT_DEPARTMENT_FAILED",
                    "Department",
                    department.Id,
                    succeeded: false,
                    errorMessage: ex.InnerException?.Message ?? ex.Message);

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
            catch(Exception ex)
            {
                // Audit log failure
                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "EDIT_DEPARTMENT_FAILED",
                    "Department",
                    department.Id,
                    succeeded: false,
                    errorMessage: ex.Message);

                throw;
            }
        }
        #endregion

        #region Delete
        [HttpGet]
        [Authorize(Policy = "RequireCEO")]
        public async Task<IActionResult> Delete(int id)
        {
            var department = await _unitOfWork.DepartmentRepository.GetByIdAsync(id);

            if (department == null)
                return NotFound();
            var departmentViewModel = _mapper.Map<DepartmentViewModel>(department);
            return View(departmentViewModel);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize(Policy = "RequireCEO")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var department = await _unitOfWork.DepartmentRepository.GetByIdAsync(id);

            if (department == null)
                return NotFound();

            string departmentName = department.DepartmentName;

            // Check if department has employees
            if (await _unitOfWork.DepartmentRepository.HasActiveEmployeesAsync(id))
            {

                // Audit log failure
                await _auditService.LogAsync(

                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "DELETE_DEPARTMENT_FAILED",
                    "Department",
                    id,
                    succeeded: false,
                    errorMessage: "Department has active employees",
                    details: $"Department: {departmentName}");
                TempData["ErrorMessage"] = $"Cannot delete {departmentName} because it has active employees.";
                return RedirectToAction(nameof(Index));
            }
            try
            {
                await _unitOfWork.DepartmentRepository.DeleteAsync(id);
                await _unitOfWork.CompleteAsync();
                await InvalidateDepartmentRelatedCachesAsync(id);

                // Audit log success

                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "DELETE_DEPARTMENT_SUCCESS",
                    "Department",
                    id,
                    details: $"Deleted department: {departmentName}");

                TempData["SuccessMessage"] = $"Department '{departmentName}' deleted successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // Audit log failure
                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "DELETE_DEPARTMENT_FAILED",
                    "Department",
                    id,
                    succeeded: false,
                    errorMessage: ex.Message,
                    details: $"Department: {departmentName}");
                TempData["ErrorMessage"] = $"An error occurred while deleting {departmentName}.";
                return RedirectToAction(nameof(Index));
            }
        }
        #endregion

        #region DepartmentEmployees
        /// <summary>
        /// View all employees in a specific department with pagination and search
        /// </summary>
        // Update the DepartmentEmployees action to include status filtering
        [HttpGet]
        [Authorize(Policy = "RequireManager")]
        public async Task<IActionResult> DepartmentEmployees(int id, int pageNumber = 1, int pageSize = 10,
                                                             string? searchTerm = null, string? status = null)
        {
            if (User.IsInRole("DepartmentManager"))
            {
                var managedDepartmentClaim = User.FindFirstValue("ManagedDepartmentId");
                if (!int.TryParse(managedDepartmentClaim, out var managedDepartmentId) || managedDepartmentId != id)
                {
                    return Unauthorized();
                }
            }

            var department = await _unitOfWork.DepartmentRepository.GetByIdAsync(id);

            if (department == null)
                return NotFound();

            // Store search parameters for view
            ViewData["SearchTerm"] = searchTerm;
            ViewData["Status"] = status;
            ViewData["DepartmentId"] = id;

            string? likePattern = null;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = InputSanitizer.NormalizeWhitespace(searchTerm);
                searchTerm = InputSanitizer.SanitizeLikeQuery(searchTerm);
                likePattern = $"%{searchTerm}%";
            }

            // Get paginated employees with filters
            var pagedEmployees = await _unitOfWork.EmployeeRepository.GetPagedAsync(
                pageNumber,
                pageSize,
                filter: e => e.DepartmentId == id &&
                            !e.IsDeleted &&
                            (status == null || status == "all" ||
                             (status == "active" && e.IsActive) ||
                             (status == "inactive" && !e.IsActive)) &&
                            (likePattern == null ||
                             EF.Functions.Like(e.FirstName, likePattern) ||
                             EF.Functions.Like(e.LastName, likePattern) ||
                             EF.Functions.Like(e.Position, likePattern) ||
                             EF.Functions.Like(e.Email, likePattern) ||
                             EF.Functions.Like(e.PhoneNumber, likePattern)),
                orderBy: q => q.OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
            );

            var employeeViewModels = _mapper.Map<List<EmployeeViewModel>>(pagedEmployees.Items);

            var pagedResult = new PagedResult<EmployeeViewModel>(
                employeeViewModels,
                pagedEmployees.TotalCount,
                pagedEmployees.PageNumber,
                pagedEmployees.PageSize
            );

            var departmentViewModel = _mapper.Map<DepartmentViewModel>(department);
            ViewBag.PagedEmployees = pagedResult;

            return View(departmentViewModel);
        }
        #endregion

        #region DepartmentProjects
        /// <summary>
        /// View all projects in a specific department with pagination and search
        /// </summary>
        [HttpGet]
        [Authorize(Policy = "RequireManager")]
        // localhost:5001/Department/DepartmentProjects/1
        public async Task<IActionResult> DepartmentProjects(int id, int pageNumber = 1, int pageSize = 10,
                                                           string? searchTerm = null, string? status = null)
        {
            var department = await _unitOfWork.DepartmentRepository.GetByIdAsync(id);

            if (department == null)
                return NotFound();

            // Store search parameters for view
            ViewData["SearchTerm"] = searchTerm;
            ViewData["Status"] = status;
            ViewData["DepartmentId"] = id;

            string? likePattern = null;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = InputSanitizer.NormalizeWhitespace(searchTerm);
                searchTerm = InputSanitizer.SanitizeLikeQuery(searchTerm);
                likePattern = $"%{searchTerm}%";
            }

            // Get paginated projects with filters
            var pagedProjects = await _unitOfWork.ProjectRepository.GetPagedAsync(
                pageNumber,
                pageSize,
                filter: p => p.DepartmentId == id &&
                            !p.IsDeleted &&
                            (status == null || status == "all" ||
                             (status == "planning" && p.Status == ProjectStatus.Planning) ||
                             (status == "inprogress" && p.Status == ProjectStatus.InProgress) ||
                             (status == "completed" && p.Status == ProjectStatus.Completed) ||
                             (status == "onhold" && p.Status == ProjectStatus.OnHold) ||
                             (status == "cancelled" && p.Status == ProjectStatus.Cancelled)) &&
                            (likePattern == null ||
                             EF.Functions.Like(p.ProjectCode, likePattern) ||
                             EF.Functions.Like(p.ProjectName, likePattern) ||
                             (p.ProjectManager != null &&
                              (EF.Functions.Like(p.ProjectManager.FirstName, likePattern) ||
                               EF.Functions.Like(p.ProjectManager.LastName, likePattern)))),
                orderBy: q => q.OrderBy(p => p.ProjectCode)
            );

            var projectViewModels = _mapper.Map<List<ProjectViewModel>>(pagedProjects.Items);

            var pagedResult = new PagedResult<ProjectViewModel>(
                projectViewModels,
                pagedProjects.TotalCount,
                pagedProjects.PageNumber,
                pagedProjects.PageSize
            );

            var departmentViewModel = _mapper.Map<DepartmentViewModel>(department);

            // Get all projects for stats (not just current page)
            var allProjects = await _unitOfWork.DepartmentRepository.GetProjectsByDepartmentAsync(id);

            ViewBag.PagedProjects = pagedResult;

            return View(departmentViewModel);
        }
        #endregion

        #region Profile

        /// <summary>
        /// Display department profile with comprehensive information
        /// </summary>
        [HttpGet]
        [Authorize(Policy = "RequireManager")]
        public async Task<IActionResult> Profile(int id)
        {
            try
            {
                // Get department with all related data
                var department = await _unitOfWork.DepartmentRepository.GetDepartmentProfileAsync(id);

                if (department == null)
                {
                    TempData["ErrorMessage"] = "Department not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Map to profile ViewModel
                var profileViewModel = _mapper.Map<DepartmentProfileViewModel>(department);

                return View(profileViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading department profile for ID {DepartmentId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the department profile.";
                return RedirectToAction(nameof(Index));
            }
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
        /// Invalidation rules:
        /// - Department dropdown cache is shared across create/edit forms.
        /// - Department-level reports depend on department writes.
        /// </summary>
        private Task InvalidateDepartmentRelatedCachesAsync(int? departmentId = null)
        {
            // Repository-level invalidation keeps department list and profile caches consistent.
            return _unitOfWork.DepartmentRepository.InvalidateCacheAsync(departmentId);
        }
        #endregion
    }
}
