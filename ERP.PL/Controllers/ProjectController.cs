using AutoMapper;
using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.PL.Helpers;
using ERP.PL.ViewModels.Project;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ERP.PL.Controllers
{
    public class ProjectController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<ProjectController> _logger;
        private readonly IConfiguration _configuration;

        public ProjectController(
            IMapper mapper,
            IUnitOfWork unitOfWork,
            ILogger<ProjectController> logger,
            IConfiguration configuration)
        {
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _configuration = configuration;
        }

        #region Index
        [HttpGet]
        public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 10,
                                                string? searchTerm = null, string? status = null)
        {
            try
            {
                // Store search parameters for view
                ViewData["SearchTerm"] = searchTerm;
                ViewData["Status"] = status ?? "all";

                string? likePattern = null;

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = InputSanitizer.NormalizeWhitespace(searchTerm);
                    searchTerm = InputSanitizer.SanitizeLikeQuery(searchTerm);
                    likePattern = $"%{searchTerm}%";
                }

                var pagedProjects = await _unitOfWork.ProjectRepository.GetPagedAsync(
                    pageNumber,
                    pageSize,
                    filter: p =>
                        (likePattern == null ||
                         EF.Functions.Like(p.ProjectCode, likePattern) ||
                         EF.Functions.Like(p.ProjectName, likePattern) ||
                         (p.Department != null && EF.Functions.Like(p.Department.DepartmentName, likePattern)) ||
                         (p.ProjectManager != null &&
                          (EF.Functions.Like(p.ProjectManager.FirstName, likePattern) ||
                           EF.Functions.Like(p.ProjectManager.LastName, likePattern)))) &&
                        (status == null || status == "all" || p.Status.ToString().ToLower() == status) &&
                        !p.IsDeleted,
                    orderBy: q => q.OrderBy(p => p.ProjectCode)
                );

                var projectViewModels = _mapper.Map<List<ProjectViewModel>>(pagedProjects.Items);

                var pagedResult = new PagedResult<ProjectViewModel>(
                    projectViewModels,
                    pagedProjects.TotalCount,
                    pagedProjects.PageNumber,
                    pagedProjects.PageSize
                );

                return View(pagedResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving projects");
                return View(new PagedResult<ProjectViewModel>(new List<ProjectViewModel>(), 0, 1, pageSize));
            }
        }
        #endregion

        #region Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadDepartmentsAsync();
            await LoadAvailableProjectManagersAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProjectViewModel project)
        {
            // Remove navigation properties from validation
            ModelState.Remove("Department");
            ModelState.Remove("ProjectManager");

            // Input sanitization
            if (!string.IsNullOrWhiteSpace(project.ProjectCode))
            {
                project.ProjectCode = InputSanitizer.NormalizeWhitespace(project.ProjectCode);
            }

            if (!string.IsNullOrWhiteSpace(project.ProjectName))
            {
                project.ProjectName = InputSanitizer.NormalizeWhitespace(project.ProjectName);
            }

            if (!string.IsNullOrWhiteSpace(project.Description))
            {
                project.Description = InputSanitizer.NormalizeWhitespace(project.Description);
            }

            // Validate model first
            if (!ModelState.IsValid)
            {
                await LoadDepartmentsAsync(project.DepartmentId);
                await LoadAvailableProjectManagersAsync(project.ProjectManagerId);
                return View(project);
            }

            // Date validation
            if (project.EndDate.HasValue && project.EndDate.Value < project.StartDate)
            {
                ModelState.AddModelError("EndDate", "End date cannot be before start date.");
                await LoadDepartmentsAsync(project.DepartmentId);
                await LoadAvailableProjectManagersAsync(project.ProjectManagerId);
                return View(project);
            }

            // Server-side project code uniqueness validation
            if (await _unitOfWork.ProjectRepository.ProjectCodeExistsAsync(project.ProjectCode))
            {
                ModelState.AddModelError("ProjectCode", "This project code is already in use.");
                await LoadDepartmentsAsync(project.DepartmentId);
                await LoadAvailableProjectManagersAsync(project.ProjectManagerId);
                return View(project);
            }

            using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                // Validate project manager if selected
                if (project.ProjectManagerId.HasValue)
                {
                    var manager = await _unitOfWork.EmployeeRepository
                        .GetByIdAsync(project.ProjectManagerId.Value);

                    if (manager == null)
                    {
                        ModelState.AddModelError("ProjectManagerId", "Selected project manager does not exist.");
                        await LoadDepartmentsAsync(project.DepartmentId);
                        await LoadAvailableProjectManagersAsync(project.ProjectManagerId);
                        return View(project);
                    }

                    // Check if manager is already managing another project
                    var conflict = await _unitOfWork.ProjectRepository
                        .GetProjectByManagerForUpdateAsync(
                            project.ProjectManagerId.Value,
                            null); // Null for new project

                    if (conflict != null)
                    {
                        ModelState.AddModelError(
                            "ProjectManagerId",
                            $"This employee is already managing project '{conflict.ProjectName}'."
                        );

                        await LoadDepartmentsAsync(project.DepartmentId);
                        await LoadAvailableProjectManagersAsync(project.ProjectManagerId);
                        return View(project);
                    }
                }

                // Create project
                var mappedProject = _mapper.Map<ERP.DAL.Models.Project>(project);

                await _unitOfWork.ProjectRepository.AddAsync(mappedProject);
                await _unitOfWork.CompleteAsync();

                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Project '{mappedProject.ProjectName}' created successfully!";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
                when (ex.InnerException?.Message.Contains("IX_Projects_ProjectManagerId_Unique") == true)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    "ProjectManagerId",
                    "This employee is already assigned as a project manager."
                );

                await LoadDepartmentsAsync(project.DepartmentId);
                await LoadAvailableProjectManagersAsync(project.ProjectManagerId);
                return View(project);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();

                var errorMessage = "An error occurred while creating the project.";
                if (ex.InnerException?.Message.Contains("CK_Project_ProjectCode_Format") == true)
                {
                    errorMessage = "Invalid project code format. Expected format: PRJ-YYYY-XXX (e.g., PRJ-2026-001).";
                    ModelState.AddModelError("ProjectCode", errorMessage);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, errorMessage);
                }

                await LoadDepartmentsAsync(project.DepartmentId);
                await LoadAvailableProjectManagersAsync(project.ProjectManagerId);
                return View(project);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating project");
                throw;
            }
        }
        #endregion

        #region Edit
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);
            if (project == null)
            {
                return NotFound();
            }

            var projectViewModel = _mapper.Map<ProjectViewModel>(project);
            await LoadDepartmentsAsync(projectViewModel.DepartmentId);
            await LoadAvailableProjectManagersAsync(projectViewModel.ProjectManagerId, id);
            return View(projectViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProjectViewModel project)
        {
            // Remove navigation-only validation
            ModelState.Remove("Department");
            ModelState.Remove("ProjectManager");

            // Validate model FIRST
            if (!ModelState.IsValid)
            {
                await LoadDepartmentsAsync(project.DepartmentId);
                await LoadAvailableProjectManagersAsync(project.ProjectManagerId, project.Id);
                return View(project);
            }

            // Date validation
            if (project.EndDate.HasValue && project.EndDate.Value < project.StartDate)
            {
                ModelState.AddModelError("EndDate", "End date cannot be before start date.");
                await LoadDepartmentsAsync(project.DepartmentId);
                await LoadAvailableProjectManagersAsync(project.ProjectManagerId, project.Id);
                return View(project);
            }

            using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                // Validate project manager if selected
                if (project.ProjectManagerId.HasValue)
                {
                    var manager = await _unitOfWork.EmployeeRepository
                        .GetByIdAsync(project.ProjectManagerId.Value);

                    if (manager == null)
                    {
                        ModelState.AddModelError("ProjectManagerId", "Selected project manager does not exist.");
                        await LoadDepartmentsAsync(project.DepartmentId);
                        await LoadAvailableProjectManagersAsync(project.ProjectManagerId, project.Id);
                        return View(project);
                    }

                    // Check for manager conflicts with locking
                    var conflict = await _unitOfWork.ProjectRepository
                        .GetProjectByManagerForUpdateAsync(
                            project.ProjectManagerId.Value,
                            project.Id); // Exclude current project

                    if (conflict != null)
                    {
                        ModelState.AddModelError(
                            "ProjectManagerId",
                            $"This employee is already managing project '{conflict.ProjectName}'."
                        );

                        await LoadDepartmentsAsync(project.DepartmentId);
                        await LoadAvailableProjectManagersAsync(project.ProjectManagerId, project.Id);
                        return View(project);
                    }
                }

                // Load existing project (TRACKED for update)
                var existingProject =
                    await _unitOfWork.ProjectRepository.GetByIdTrackedAsync(project.Id);

                if (existingProject == null)
                    return NotFound();

                // Apply allowed updates
                _mapper.Map(project, existingProject);
                _unitOfWork.ProjectRepository.Update(existingProject);

                // Commit DB changes
                await _unitOfWork.CompleteAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Project '{existingProject.ProjectName}' updated successfully!";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
                when (ex.InnerException?.Message.Contains("IX_Projects_ProjectManagerId_Unique") == true)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    "ProjectManagerId",
                    "This employee is already assigned as a project manager."
                );

                await LoadDepartmentsAsync(project.DepartmentId);
                await LoadAvailableProjectManagersAsync(project.ProjectManagerId, project.Id);
                return View(project);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();

                var errorMessage = "An error occurred while saving the project.";
                if (ex.InnerException?.Message.Contains("CK_Project_ProjectCode_Format") == true)
                {
                    errorMessage = "Invalid project code format. Expected format: PRJ-YYYY-XXX (e.g., PRJ-2026-001).";
                    ModelState.AddModelError("ProjectCode", errorMessage);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, errorMessage);
                }

                await LoadDepartmentsAsync(project.DepartmentId);
                await LoadAvailableProjectManagersAsync(project.ProjectManagerId, project.Id);
                return View(project);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating project");
                throw;
            }
        }
        #endregion

        #region Delete
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);

            if (project == null)
                return NotFound();

            var projectViewModel = _mapper.Map<ProjectViewModel>(project);
            return View(projectViewModel);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);

            if (project == null)
                return NotFound();

            await _unitOfWork.ProjectRepository.DeleteAsync(id);
            await _unitOfWork.CompleteAsync();

            TempData["SuccessMessage"] = $"Project '{project.ProjectName}' deleted successfully!";
            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region ProjectEmployees
        /// <summary>
        /// View all employees assigned to a specific project with pagination and search
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ProjectEmployees(int id, int pageNumber = 1, int pageSize = 10,
                                                         string? searchTerm = null, string? status = null)
        {
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);

            if (project == null)
                return NotFound();

            // Store search parameters for view
            ViewData["SearchTerm"] = searchTerm;
            ViewData["Status"] = status;
            ViewData["ProjectId"] = id;

            string? likePattern = null;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = InputSanitizer.NormalizeWhitespace(searchTerm);
                searchTerm = InputSanitizer.SanitizeLikeQuery(searchTerm);
                likePattern = $"%{searchTerm}%";
            }

            // Get paginated employees assigned to this project with filters
            var employees = await _unitOfWork.ProjectRepository.GetEmployeesByProjectQueryableAsync(id);

            // Apply filters manually since we're getting all employees for the project
            var filteredEmployees = employees
                .Where(e => !e.IsDeleted &&
                           (status == null || status == "all" ||
                            (status == "active" && e.IsActive) ||
                            (status == "inactive" && !e.IsActive)) &&
                           (likePattern == null ||
                            EF.Functions.Like(e.FirstName, likePattern) ||
                            EF.Functions.Like(e.LastName, likePattern) ||
                            EF.Functions.Like(e.Position, likePattern) ||
                            EF.Functions.Like(e.Email, likePattern) ||
                            EF.Functions.Like(e.PhoneNumber, likePattern) ||
                            (e.Department != null && EF.Functions.Like(e.Department.DepartmentName, likePattern))))
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToList();

            // Manual pagination since we're filtering in memory
            var totalCount = filteredEmployees.Count;
            var pagedItems = filteredEmployees
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var employeeViewModels = _mapper.Map<List<ERP.PL.ViewModels.Employee.EmployeeViewModel>>(pagedItems);

            var pagedResult = new PagedResult<ERP.PL.ViewModels.Employee.EmployeeViewModel>(
                employeeViewModels,
                totalCount,
                pageNumber,
                pageSize
            );

            var projectViewModel = _mapper.Map<ProjectViewModel>(project);
            projectViewModel.AssignedEmployees = filteredEmployees;
            ViewBag.PagedEmployees = pagedResult;

            return View(projectViewModel);
        }
        #endregion

        #region Profile
        /// <summary>
        /// Display project profile page with comprehensive information
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Profile(int id)
        {
            try
            {
                // Get project with all related data
                var project = await _unitOfWork.ProjectRepository.GetProjectProfileAsync(id);

                if (project == null)
                {
                    TempData["ErrorMessage"] = "Project not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Map to profile view model
                var profileViewModel = _mapper.Map<ProjectProfileViewModel>(project);

                return View(profileViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading project profile for ID {ProjectId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the project profile.";
                return RedirectToAction(nameof(Index));
            }
        }
        #endregion

        #region Remote Validation
        /// <summary>
        /// Remote validation for project code uniqueness
        /// </summary>
        [AcceptVerbs("GET", "POST")]
        public async Task<IActionResult> IsProjectCodeUnique(string projectCode, int? id)
        {
            if (string.IsNullOrWhiteSpace(projectCode))
                return Json(true);

            var exists = await _unitOfWork.ProjectRepository.ProjectCodeExistsAsync(projectCode, id);
            return Json(!exists);
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Load departments for dropdown
        /// </summary>
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
        /// Load available project managers (employees not already managing a project)
        /// </summary>
        private async Task LoadAvailableProjectManagersAsync(int? currentManagerId = null, int? currentProjectId = null)
        {
            var employees = await _unitOfWork.EmployeeRepository.GetAllAsync();
            var projects = await _unitOfWork.ProjectRepository.GetAllAsync();

            // Get all employees who are already managing a project (except current)
            var managingEmployeeIds = projects
                .Where(p => p.ProjectManagerId.HasValue && p.Id != currentProjectId)
                .Select(p => p.ProjectManagerId!.Value)
                .ToHashSet();

            // Filter available managers: active, not deleted, not managing another project
            var availableManagers = employees
                .Where(e => e.IsActive &&
                           !e.IsDeleted &&
                           !managingEmployeeIds.Contains(e.Id))
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName);

            ViewBag.ProjectManagers = new SelectList(
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
        #endregion
    }
}
