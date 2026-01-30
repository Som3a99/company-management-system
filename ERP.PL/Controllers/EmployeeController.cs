using AutoMapper;
using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Models;
using ERP.PL.Helpers;
using ERP.PL.ViewModels.Employee;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ERP.PL.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly DocumentSettings _documentSettings;
        private readonly ILogger<EmployeeController> _logger;
        private readonly IConfiguration _configuration;


        public EmployeeController(IMapper mapper, IUnitOfWork unitOfWork, DocumentSettings documentSettings, ILogger<EmployeeController> logger, IConfiguration configuration)
        {
            _mapper=mapper;
            _unitOfWork=unitOfWork;
            _documentSettings=documentSettings;
            _logger=logger;
            _configuration=configuration;
        }

        #region Index
        [HttpGet]
        public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 10, string? searchTerm = null)
        {
            try
            {
                // Store search term in ViewData for the view
                ViewData["SearchTerm"] = searchTerm;

                string? likePattern = null;

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = InputSanitizer.NormalizeWhitespace(searchTerm);
                    searchTerm = InputSanitizer.SanitizeLikeQuery(searchTerm);
                    likePattern = $"%{searchTerm}%";
                }

                var pagedEmployees = await _unitOfWork.EmployeeRepository.GetPagedAsync(
                    pageNumber,
                    pageSize,
                    filter: e =>
                        likePattern == null ||
                        EF.Functions.Like(e.FirstName, likePattern) ||
                        EF.Functions.Like(e.LastName, likePattern) ||
                        EF.Functions.Like(e.Email, likePattern) ||
                        EF.Functions.Like(e.Position, likePattern) ||
                        (e.Department != null &&
                         EF.Functions.Like(e.Department.DepartmentName, likePattern)),
                    orderBy: q => q.OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                );

                // Map to view models
                var employeeViewModels = _mapper.Map<List<EmployeeViewModel>>(pagedEmployees.Items);

                var pagedResult = new PagedResult<EmployeeViewModel>(
                    employeeViewModels,
                    pagedEmployees.TotalCount,
                    pagedEmployees.PageNumber,
                    pagedEmployees.PageSize
                );

                return View(pagedResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving employees");
                TempData["ErrorMessage"] = "An error occurred while loading employees.";
                return View(new PagedResult<EmployeeViewModel>(new List<EmployeeViewModel>(), 0, 1, pageSize));
            }
        }
        #endregion

        #region Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadDepartmentsAsync();
            return View();
        }

        [HttpPost]
        [RequestSizeLimit(10 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeViewModel employee)
        {
            ModelState.Remove("Department");
            ModelState.Remove("Image");
            ModelState.Remove("ImageUrl");

            if (!ModelState.IsValid)
            {
                await LoadDepartmentsAsync();
                return View(employee);
            }
            // Phone number sanitization and validation
            if (!string.IsNullOrWhiteSpace(employee.PhoneNumber))
            {
                var sanitizedPhone = InputSanitizer.SanitizePhoneNumber(employee.PhoneNumber);
                if (sanitizedPhone == null)
                {
                    ModelState.AddModelError("PhoneNumber", "Invalid phone number format.");
                    await LoadDepartmentsAsync();
                    return View(employee);
                }
                employee.PhoneNumber = sanitizedPhone;
            }

            // Salary range validation from appsettings
            var salaryMin = _configuration.GetValue<decimal>("Salary:Min", 30000);
            var salaryMax = _configuration.GetValue<decimal>("Salary:Max", 500000);

            if (employee.Salary < salaryMin || employee.Salary > salaryMax)
            {
                ModelState.AddModelError("Salary",
                    $"Salary must be between {salaryMin:C} and {salaryMax:C}. Current value: {employee.Salary:C}");
                await LoadDepartmentsAsync();
                return View(employee);
            }
            // Server-side email uniqueness validation
            if (await _unitOfWork.EmployeeRepository.EmailExistsAsync(employee.Email))
            {
                ModelState.AddModelError("Email", "This email address is already registered.");
                await LoadDepartmentsAsync();
                return View(employee);
            }

            // Start transaction BEFORE any file operations
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            string? tempImagePath = null;
            string? finalImagePath = null;

            try
            {
                var employeeMapped = _mapper.Map<Employee>(employee);

                if (employee.Image != null && employee.Image.Length > 0)
                {
                    // Upload to temporary location with GUID name
                    var tempFileName = $"temp_{Guid.NewGuid()}_{employee.Image.FileName}";
                    tempImagePath = await _documentSettings.UploadImageToTempPath(employee.Image, tempFileName);

                    // Set the path as temporary for now
                    employeeMapped.ImageUrl = tempImagePath;
                }
                else
                {
                    // Gender-based default avatar
                    employeeMapped.ImageUrl = _documentSettings.GetDefaultAvatarByGender(employeeMapped.Gender);
                }

                // Save employee to get ID
                await _unitOfWork.EmployeeRepository.AddAsync(employeeMapped);
                await _unitOfWork.CompleteAsync();

                // FIX: If we have a temp image, move it to final location with employee ID
                if (!string.IsNullOrEmpty(tempImagePath) && employeeMapped.Id > 0)
                {
                    var originalFileName = employee.Image?.FileName ?? string.Empty;
                    finalImagePath = await _documentSettings.MoveImageToFinalLocation(
                        tempImagePath,
                        employeeMapped.Id,
                        originalFileName);

                    // Update employee with final image path
                    employeeMapped.ImageUrl = finalImagePath;
                    _unitOfWork.EmployeeRepository.Update(employeeMapped);
                    await _unitOfWork.CompleteAsync();
                }

                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Employee '{employeeMapped.FirstName} {employeeMapped.LastName}' created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                await transaction.RollbackAsync();

                // Clean up temp file if exists
                if (!string.IsNullOrEmpty(tempImagePath))
                {
                    try { _documentSettings.DeleteTempImage(tempImagePath); } catch { }
                }

                ModelState.AddModelError("Email", "This email address is already registered.");
                await LoadDepartmentsAsync();
                return View(employee);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                // Clean up temp file if exists
                if (!string.IsNullOrEmpty(tempImagePath))
                {
                    try { _documentSettings.DeleteTempImage(tempImagePath); } catch { }
                }

                _logger.LogError(ex, "Error creating employee");
                ModelState.AddModelError(string.Empty, "An error occurred while creating the employee.");
                await LoadDepartmentsAsync();
                return View(employee);
            }
        }
        #endregion

        #region Edit
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var employee = await _unitOfWork.EmployeeRepository.GetByIdAsync(id);
            if (employee == null)
                return NotFound();
            
            await LoadDepartmentsAsync(employee.DepartmentId);
            await LoadAvailableProjectsAsync(employee.ProjectId);


            var employeeViewModel = _mapper.Map<EmployeeViewModel>(employee);
            return View(employeeViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EmployeeViewModel viewModel)
        {
            // Remove navigation properties from validation
            ModelState.Remove("Department");
            ModelState.Remove("Image"); // Optional
            ModelState.Remove("ImageUrl"); // Set by controller
            ModelState.Remove("Project");

            if (!ModelState.IsValid)
            {
                await LoadDepartmentsAsync(viewModel.DepartmentId);
                await LoadAvailableProjectsAsync(viewModel.ProjectId);
                return View(viewModel);
            }
            // FIX #13: Phone number sanitization and validation
            if (!string.IsNullOrWhiteSpace(viewModel.PhoneNumber))
            {
                var sanitizedPhone = InputSanitizer.SanitizePhoneNumber(viewModel.PhoneNumber);
                if (sanitizedPhone == null)
                {
                    ModelState.AddModelError("PhoneNumber", "Invalid phone number format.");
                    await LoadDepartmentsAsync(viewModel.DepartmentId);
                    await LoadAvailableProjectsAsync(viewModel.ProjectId);
                    return View(viewModel);
                }
                viewModel.PhoneNumber = sanitizedPhone;
            }

            // FIX #14: Salary range validation from appsettings
            var salaryMin = _configuration.GetValue<decimal>("Salary:Min", 30000);
            var salaryMax = _configuration.GetValue<decimal>("Salary:Max", 500000);

            if (viewModel.Salary < salaryMin || viewModel.Salary > salaryMax)
            {
                ModelState.AddModelError("Salary",
                    $"Salary must be between {salaryMin:C} and {salaryMax:C}. Current value: {viewModel.Salary:C}");
                await LoadDepartmentsAsync(viewModel.DepartmentId);
                await LoadAvailableProjectsAsync(viewModel.ProjectId);
                return View(viewModel);
            }
            // CRITICAL: Load existing entity from database (TRACKED for update)
            var existingEmployee = await _unitOfWork.EmployeeRepository.GetByIdTrackedAsync(viewModel.Id);

            if (existingEmployee == null)
                return NotFound();

            // Validate project assignment change
            if (viewModel.ProjectId.HasValue && viewModel.ProjectId != existingEmployee.ProjectId)
            {
                // Check if the project exists
                var project = await _unitOfWork.ProjectRepository.GetByIdAsync(viewModel.ProjectId.Value);
                if (project == null)
                {
                    ModelState.AddModelError("ProjectId", "Selected project does not exist.");
                    await LoadDepartmentsAsync(viewModel.DepartmentId);
                    await LoadAvailableProjectsAsync(viewModel.ProjectId);
                    return View(viewModel);
                }

                // Check if employee is already assigned to another project
                var isAssigned = await _unitOfWork.ProjectRepository
                    .IsEmployeeAssignedToProjectAsync(viewModel.Id, viewModel.ProjectId);

                if (isAssigned)
                {
                    var currentProject = await _unitOfWork.EmployeeRepository
                        .GetByIdAsync(viewModel.Id);

                    ModelState.AddModelError("ProjectId",
                        $"This employee is already assigned to another project.");
                    await LoadDepartmentsAsync(viewModel.DepartmentId);
                    await LoadAvailableProjectsAsync(viewModel.ProjectId);
                    return View(viewModel);
                }
            }

            // Handle image logic BEFORE mapping (manual control)
            if (viewModel.Image != null && viewModel.Image.Length > 0)
            {
                try
                {
                    // Delete old image if it exists and is not a default avatar
                    if (!_documentSettings.IsDefaultAvatar(existingEmployee.ImageUrl))
                    {
                        _documentSettings.DeleteImage(existingEmployee.ImageUrl, "images");
                    }

                    // Upload new image
                    existingEmployee.ImageUrl = await _documentSettings.UploadImagePath(viewModel.Image, "images");
                }
                catch (ArgumentException ex)
                {
                    ModelState.AddModelError("Image", ex.Message);
                    await LoadDepartmentsAsync(viewModel.DepartmentId);
                    await LoadAvailableProjectsAsync(viewModel.ProjectId);
                    return View(viewModel);
                }
            }
            // If gender changed and using default avatar, update to new gender's default
            else if (viewModel.Gender != existingEmployee.Gender &&
                     _documentSettings.IsDefaultAvatar(existingEmployee.ImageUrl))
            {
                existingEmployee.ImageUrl = _documentSettings.GetDefaultAvatarByGender(viewModel.Gender);
            }

            // Map ViewModel properties ONTO existing tracked entity
            // This preserves ImageUrl and other properties configured to be ignored
            _mapper.Map(viewModel, existingEmployee);

            // Update and save
            try
            {
                _unitOfWork.EmployeeRepository.Update(existingEmployee);
                await _unitOfWork.CompleteAsync();

                TempData["SuccessMessage"] =
                    $"Employee '{existingEmployee.FirstName} {existingEmployee.LastName}' updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                ModelState.AddModelError("Email", "This email address is already registered.");
                await LoadDepartmentsAsync(viewModel.DepartmentId);
                await LoadAvailableProjectsAsync(viewModel.ProjectId);
                return View(viewModel);
            }
        }
        #endregion

        #region Delete

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var employee = await _unitOfWork.EmployeeRepository.GetByIdAsync(id);
            if (employee == null)
                return NotFound();

            var employeeViewModel = _mapper.Map<EmployeeViewModel>(employee);
            return View(employeeViewModel);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            string? imageUrlToDelete = null;
            bool isDefaultAvatar = false;

            try
            {
                // Load employee (tracked)
                var employee = await _unitOfWork.EmployeeRepository.GetByIdAsync(id);
                if (employee == null)
                    return NotFound();

                // Business rule: cannot delete department manager
                if (employee.ManagedDepartment != null)
                {
                    TempData["ErrorMessage"] =
                        $"Cannot delete '{employee.FirstName} {employee.LastName}' because they are managing " +
                        $"the '{employee.ManagedDepartment.DepartmentName}' department. Assign another manager first.";

                    return RedirectToAction(nameof(Index));
                }

                // Capture image info BEFORE deletion
                imageUrlToDelete = employee.ImageUrl;
                isDefaultAvatar = _documentSettings.IsDefaultAvatar(imageUrlToDelete);

                // Delete employee (soft delete)
                await _unitOfWork.EmployeeRepository.DeleteAsync(id);
                await _unitOfWork.CompleteAsync();

                // DB succeeded → now cleanup file (OUTSIDE transaction)
                if (!string.IsNullOrWhiteSpace(imageUrlToDelete) && !isDefaultAvatar)
                {
                    try
                    {
                        _documentSettings.DeleteImage(imageUrlToDelete, "images");
                    }
                    catch (Exception ex)
                    {
                        // Non-fatal: DB is already consistent
                        _logger.LogWarning(ex,
                            "Employee {EmployeeId} deleted but image cleanup failed: {ImageUrl}",
                            id, imageUrlToDelete);
                    }
                }

                TempData["SuccessMessage"] =
                    $"Employee '{employee.FirstName} {employee.LastName}' deleted successfully!";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error deleting employee {EmployeeId}", id);

                TempData["ErrorMessage"] =
                    "An unexpected error occurred while deleting the employee.";

                return RedirectToAction(nameof(Index));
            }
        }
        #endregion

        #region Remote Validation

        /// <summary>
        /// Remote validation for email uniqueness
        /// Called by jQuery Unobtrusive Validation on client-side
        /// </summary>
        [AcceptVerbs("GET", "POST")]
        public async Task<IActionResult> IsEmailUnique(string email, int? id)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Json(true); // Required validation will catch this

            var exists = await _unitOfWork.EmployeeRepository.EmailExistsAsync(email, id);
            return Json(!exists);
        }

        #endregion

        #region Helper Method

        // Helper method to load departments for dropdown
        private async Task LoadDepartmentsAsync(int? selectedDepartmentId = null)
        {
            var departments = await _unitOfWork.DepartmentRepository.GetAllAsync();
            ViewBag.Departments = new SelectList(
                departments.Select(d => new
                {
                    d.Id,
                    DisplayText = $"{d.DepartmentCode} - {d.DepartmentName}"
                }),
                "Id",
                "DisplayText",
                selectedDepartmentId
            );
        }

        /// <summary>
        /// Load available projects for employee assignment
        /// </summary>
        private async Task LoadAvailableProjectsAsync(int? selectedProjectId = null)
        {
            var allProjects = await _unitOfWork.ProjectRepository.GetAllAsync();

            // Filter to show only active projects (not deleted)
            var availableProjects = allProjects
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.ProjectCode);

            ViewBag.Projects = new SelectList(
                availableProjects.Select(p => new
                {
                    p.Id,
                    DisplayText = $"{p.ProjectCode} - {p.ProjectName}"
                }),
                "Id",
                "DisplayText",
                selectedProjectId
            );
        }
        #endregion

    }
}
