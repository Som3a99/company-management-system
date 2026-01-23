using AutoMapper;
using ERP.BLL.Interfaces;
using ERP.DAL.Models;
using ERP.PL.Helpers;
using ERP.PL.ViewModels.Employee;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

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
        public async Task<IActionResult> Index()
        {
            var employees = await _unitOfWork.EmployeeRepository.GetAllAsync();
            var employeeViewModels = _mapper.Map<IEnumerable<EmployeeViewModel>>(employees);
            return View(employeeViewModels);
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

            await _unitOfWork.EmployeeRepository.AddAsync(employeeMapped);
            await _unitOfWork.CompleteAsync();

            TempData["SuccessMessage"] = $"Employee '{employeeMapped.FirstName} {employeeMapped.LastName}' created successfully!";
            return RedirectToAction(nameof(Index));
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

            // CRITICAL: Load existing entity from database
            var existingEmployee = await _unitOfWork.EmployeeRepository.GetByIdAsync(viewModel.Id);

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
            _unitOfWork.EmployeeRepository.Update(existingEmployee);
            await _unitOfWork.CompleteAsync();

            TempData["SuccessMessage"] = $"Employee '{existingEmployee.FirstName} {existingEmployee.LastName}' updated successfully!";
            return RedirectToAction(nameof(Index));
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
            // Use transaction for atomic operation
            using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                var employee = await _unitOfWork.EmployeeRepository.GetByIdAsync(id);
                if (employee == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound();
                }

                // Check if employee is managing a department
                if (employee.ManagedDepartment != null)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = $"Cannot delete '{employee.FirstName} {employee.LastName}' " +
                        $"because they are currently managing the '{employee.ManagedDepartment.DepartmentName}' department. " +
                        $"Please assign a new manager first.";
                    return RedirectToAction(nameof(Index));
                }

                // Store image info before deletion
                var imageUrl = employee.ImageUrl;
                var isDefaultAvatar = _documentSettings.IsDefaultAvatar(imageUrl);

                // Soft delete the employee
                _unitOfWork.EmployeeRepository.Delete(id);

                // Save changes to database
                var result = await _unitOfWork.CompleteAsync();

                if (result > 0)
                {
                    // Only delete physical file if database deletion succeeded
                    if (!string.IsNullOrEmpty(imageUrl) && !isDefaultAvatar)
                    {
                        try
                        {
                            _documentSettings.DeleteImage(imageUrl, "images");
                        }
                        catch (Exception ex)
                        {
                            // Log but don't fail the operation if image deletion fails
                            _logger.LogWarning(ex,
                                "Failed to delete image file {ImageUrl} for employee {EmployeeId}",
                                imageUrl, id);
                        }
                    }

                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = $"Employee '{employee.FirstName} {employee.LastName}' deleted successfully!";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = "Failed to delete employee. Please try again.";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error deleting employee {EmployeeId}", id);

                TempData["ErrorMessage"] = "An error occurred while deleting the employee. Please try again.";
                return RedirectToAction(nameof(Index));
            }
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
