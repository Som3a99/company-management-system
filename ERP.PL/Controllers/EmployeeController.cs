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

        public EmployeeController(IMapper mapper, IUnitOfWork unitOfWork, DocumentSettings documentSettings, ILogger<EmployeeController> logger)
        {
            _mapper=mapper;
            _unitOfWork=unitOfWork;
            _documentSettings=documentSettings;
            _logger=logger;
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
            ModelState.Remove("Image"); // Remove because it's optional
            ModelState.Remove("ImageUrl"); // Remove because controller sets i

            if (!ModelState.IsValid)
            {
                await LoadDepartmentsAsync();
                return View(employee);
            }
            var employeeMapped = _mapper.Map<Employee>(employee);
            if (employee.Image != null && employee.Image.Length > 0)
            {
                try
                {
                    employeeMapped.ImageUrl =
                        await _documentSettings.UploadImagePath(employee.Image, "images");
                }
                catch (ArgumentException ex)
                {
                    ModelState.AddModelError("Image", ex.Message);
                    await LoadDepartmentsAsync();
                    return View(employee);
                }
            }
            else
            {
                // Gender-based default avatar
                employeeMapped.ImageUrl =
                    _documentSettings.GetDefaultAvatarByGender(employeeMapped.Gender);
            }

            try
            {
                await _unitOfWork.EmployeeRepository.AddAsync(employeeMapped);
                await _unitOfWork.CompleteAsync();

                TempData["SuccessMessage"] = $"Employee '{employeeMapped.FirstName} {employeeMapped.LastName}' created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sqlEx &&
                (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                ModelState.AddModelError("Email", "This email address is already registered.");
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

            if (!ModelState.IsValid)
            {
                await LoadDepartmentsAsync(viewModel.DepartmentId);
                return View(viewModel);
            }

            // CRITICAL: Load existing entity from database (TRACKED for update)
            var existingEmployee = await _unitOfWork.EmployeeRepository.GetByIdTrackedAsync(viewModel.Id);

            if (existingEmployee == null)
                return NotFound();

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
        #endregion

    }
}
