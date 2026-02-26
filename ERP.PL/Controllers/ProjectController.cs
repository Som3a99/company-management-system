using AutoMapper;
using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.PL.Helpers;
using ERP.PL.Services;
using ERP.PL.ViewModels.Project;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERP.PL.Controllers
{
    [Authorize]
    public class ProjectController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<ProjectController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IAuditService _auditService;
        private readonly IRoleManagementService _roleManagementService;
        private readonly IProjectTeamService _projectTeamService;
        private readonly ICacheService _cacheService;
        private readonly IProjectForecastService _projectForecastService;


        public ProjectController(
            IMapper mapper,
            IUnitOfWork unitOfWork,
            ILogger<ProjectController> logger,
            IConfiguration configuration,
            IAuditService auditService,
            IRoleManagementService roleManagementService,
            IProjectTeamService projectTeamService,
            ICacheService cacheService,
            IProjectForecastService projectForecastService)
        {
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _configuration = configuration;
            _auditService=auditService;
            _roleManagementService=roleManagementService;
            _projectTeamService=projectTeamService;
            _cacheService=cacheService;
            _projectForecastService = projectForecastService;
        }

        #region Index
        [HttpGet]
        [Authorize(Policy = "RequireManager")]
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
        [Authorize(Policy = "RequireCEO")]
        public async Task<IActionResult> Create()
        {
            await LoadDepartmentsAsync();
            await LoadAvailableProjectManagersAsync();
            return View();
        }

        [HttpPost]
        [Authorize(Policy = "RequireCEO")]
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

                // Create project inside execution-strategy-safe transaction
                var mappedProject = _mapper.Map<ERP.DAL.Models.Project>(project);

                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    await _unitOfWork.ProjectRepository.AddAsync(mappedProject);
                    await _unitOfWork.CompleteAsync();
                });

                await InvalidateProjectRelatedCachesAsync(mappedProject.Id);

                // Audit log success (outside transaction — non-critical)
                try
                {
                    await _auditService.LogAsync(
                        User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                        User.Identity!.Name!,
                        "CREATE_PROJECT_SUCCESS",
                        "Project",
                        mappedProject.Id,
                        details: $"Created project: {mappedProject.ProjectCode} - {mappedProject.ProjectName}");
                }
                catch { /* audit failure is non-critical */ }

                TempData["SuccessMessage"] =
                    $"Project '{mappedProject.ProjectName}' created successfully!";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
                when (ex.InnerException?.Message.Contains("IX_Projects_ProjectManagerId_Unique") == true)
            {
                // Audit log failure
                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "CREATE_PROJECT_FAILED",
                    "Project",
                    null,
                    succeeded: false,
                    errorMessage: "Project manager already assigned",
                    details: $"Project code: {project.ProjectCode}");



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
                // Audit log failure
                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "CREATE_PROJECT_FAILED",
                    "Project",
                    null,
                    succeeded: false,
                    errorMessage: ex.InnerException?.Message ?? ex.Message);

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
                // Audit log failure
                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "CREATE_PROJECT_FAILED",
                    "Project",
                    null,
                    succeeded: false,
                    errorMessage: ex.Message);

                _logger.LogError(ex, "Error creating project");
                throw;
            }
        }
        #endregion

        #region Edit
        [HttpGet]
        [Authorize(Roles = "CEO,ProjectManager")]
        public async Task<IActionResult> Edit(int id)
        {
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);
            if (project == null)
            {
                return NotFound();
            }

            if (!await CanAccessProjectAsync(project))
            {
                return Forbid();
            }

            var projectViewModel = _mapper.Map<ProjectViewModel>(project);
            await LoadDepartmentsAsync(projectViewModel.DepartmentId);
            await LoadAvailableProjectManagersAsync(projectViewModel.ProjectManagerId, id);
            return View(projectViewModel);
        }

        [HttpPost]
        [Authorize(Roles = "CEO,ProjectManager")]
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


                if (!await CanAccessProjectAsync(existingProject))
                {
                    return Forbid();
                }

                if (User.IsInRole("ProjectManager") && !User.IsInRole("CEO"))
                {
                    project.ProjectManagerId = existingProject.ProjectManagerId;
                }

                // Apply allowed updates
                _mapper.Map(project, existingProject);
                _unitOfWork.ProjectRepository.Update(existingProject);

                // Commit DB changes inside execution-strategy-safe transaction
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    await _unitOfWork.CompleteAsync();
                });

                await InvalidateProjectRelatedCachesAsync(existingProject.Id);

                // AUTO-ASSIGN ROLE IF MANAGER CHANGED
                if (existingProject.ProjectManagerId.HasValue)
                {
                    await _roleManagementService.SyncEmployeeRolesAsync(existingProject.ProjectManagerId.Value);
                }

                // Audit log success (outside transaction — non-critical)
                try
                {
                    await _auditService.LogAsync(
                        User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                        User.Identity!.Name!,
                        "EDIT_PROJECT_SUCCESS",
                        "Project",
                        existingProject.Id,
                        details: $"Updated project: {existingProject.ProjectCode} - {existingProject.ProjectName}");
                }
                catch { /* audit failure is non-critical */ }

                TempData["SuccessMessage"] =
                    $"Project '{existingProject.ProjectName}' updated successfully!";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
                when (ex.InnerException?.Message.Contains("IX_Projects_ProjectManagerId_Unique") == true)
            {
                // Audit log failure
                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "EDIT_PROJECT_FAILED",
                    "Project",
                    project.Id,
                    succeeded: false,
                    errorMessage: "Project manager already assigned");

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
                // Audit log failure
                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "EDIT_PROJECT_FAILED",
                    "Project",
                    project.Id,
                    succeeded: false,
                    errorMessage: ex.InnerException?.Message ?? ex.Message);

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
                // Audit log failure
                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "EDIT_PROJECT_FAILED",
                    "Project",
                    project.Id,
                    succeeded: false,
                    errorMessage: ex.Message);

                _logger.LogError(ex, "Error updating project");
                throw;
            }
        }
        #endregion

        #region Delete
        [HttpGet]
        [Authorize(Policy = "RequireCEO")]
        public async Task<IActionResult> Delete(int id)
        {
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);

            if (project == null)
                return NotFound();

            var projectViewModel = _mapper.Map<ProjectViewModel>(project);
            return View(projectViewModel);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize(Policy = "RequireCEO")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);

            if (project == null)
                return NotFound();

            string projectName = project.ProjectName;

            try
            {
                await _unitOfWork.ProjectRepository.DeleteAsync(id);
                await _unitOfWork.CompleteAsync();
                await InvalidateProjectRelatedCachesAsync(id);

                // Audit log success
                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "DELETE_PROJECT_SUCCESS",
                    "Project",
                    id,
                    details: $"Deleted project: {projectName}");

                TempData["SuccessMessage"] = $"Project '{projectName}' deleted successfully!";
                return RedirectToAction(nameof(Index));

            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project {ProjectId}", id);
                
                // Audit log failure
                await _auditService.LogAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    User.Identity!.Name!,
                    "DELETE_PROJECT_FAILED",
                    "Project",
                    id,
                    succeeded: false,
                    errorMessage: ex.Message);

                TempData["ErrorMessage"] = "An error occurred while deleting the project.";
                return RedirectToAction(nameof(Index));

            }
        }
        #endregion

        #region ProjectEmployees
        /// <summary>
        /// View all employees assigned to a specific project with pagination and search
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "CEO,ProjectManager")]
        public async Task<IActionResult> ProjectEmployees(int id, int pageNumber = 1, int pageSize = 10,
                                                         string? searchTerm = null, string? status = null)
        {
            var project = await _unitOfWork.ProjectRepository.GetByIdAsync(id);

            if (project == null)
                return NotFound();

            if (!await CanAccessProjectAsync(project))
            {
                return Forbid();
            }

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

            var currentEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;
            if (int.TryParse(currentEmployeeIdClaim, out var currentEmployeeId))
            {
                var eligibleEmployees = await _projectTeamService.GetEligibleEmployeesAsync(id, currentEmployeeId, User.IsInRole("CEO"));
                ViewBag.EligibleEmployees = new SelectList(
                    eligibleEmployees.Select(e => new { e.Id, Display = $"{e.FirstName} {e.LastName} - {e.Position}" }),
                    "Id",
                    "Display");
            }
            else
            {
                ViewBag.EligibleEmployees = new SelectList(Enumerable.Empty<object>());
            }

            return View(projectViewModel);
        }
        #endregion

        #region Manage Team
        [HttpGet]
        [Authorize(Roles = "CEO,ProjectManager")]
        public async Task<IActionResult> ManageTeam(int id)
        {
            var currentEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;
            if (!int.TryParse(currentEmployeeIdClaim, out var currentEmployeeId))
            {
                return Forbid();
            }

            var isCeo = User.IsInRole("CEO");
            var eligibleEmployees = await _projectTeamService.GetEligibleEmployeesAsync(id, currentEmployeeId, isCeo);

            ViewBag.ProjectId = id;
            ViewBag.EligibleEmployees = new SelectList(
                eligibleEmployees.Select(e => new { e.Id, Display = $"{e.FirstName} {e.LastName} - {e.Position}" }),
                "Id",
                "Display");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CEO,ProjectManager")]
        public async Task<IActionResult> AssignEmployee(int projectId, int employeeId)
        {
            var currentEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;
            if (!int.TryParse(currentEmployeeIdClaim, out var currentEmployeeId))
            {
                return Forbid();
            }

            var result = await _projectTeamService.AssignEmployeeAsync(
                projectId,
                employeeId,
                currentEmployeeId,
                User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                User.Identity?.Name ?? "Unknown",
                User.IsInRole("CEO"));

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Message;
            }
            else
            {
                TempData["SuccessMessage"] = result.Message;
            }

            return RedirectToAction(nameof(ProjectEmployees), new { id = projectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CEO,ProjectManager")]
        public async Task<IActionResult> RemoveEmployee(int projectId, int employeeId)
        {
            var currentEmployeeIdClaim = User.FindFirst("EmployeeId")?.Value;
            if (!int.TryParse(currentEmployeeIdClaim, out var currentEmployeeId))
            {
                return Forbid();
            }

            var result = await _projectTeamService.RemoveEmployeeAsync(
                projectId,
                employeeId,
                currentEmployeeId,
                User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                User.Identity?.Name ?? "Unknown",
                User.IsInRole("CEO"));

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Message;
            }
            else
            {
                TempData["SuccessMessage"] = result.Message;
            }

            return RedirectToAction(nameof(ProjectEmployees), new { id = projectId });
        }
        #endregion

        #region Profile
        /// <summary>
        /// Display project profile page with comprehensive information
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "CEO,ProjectManager,Employee")]
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

                if (!await CanAccessProjectAsync(project))
                {
                    return Forbid();
                }

                // Map to profile view model
                var profileViewModel = _mapper.Map<ProjectProfileViewModel>(project);

                // Get AI forecast
                try
                {
                    profileViewModel.Forecast = await _projectForecastService.ForecastAsync(id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load forecast for project {ProjectId}", id);
                }

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
            var departments = await _cacheService.GetOrCreateSafeAsync(
                                CacheKeys.DepartmentsAll,
                                async () => (await _unitOfWork.DepartmentRepository.GetAllAsync()).ToList(),
                                TimeSpan.FromMinutes(10));
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
            var employees = await _cacheService.GetOrCreateSafeAsync(
                CacheKeys.AvailableProjectManagersAll,
                async () => (await _unitOfWork.EmployeeRepository.GetAllAsync())
                    .Where(e => e.IsActive && !e.IsDeleted)
                    .ToList(),
                TimeSpan.FromMinutes(10));

            var projects = await _cacheService.GetOrCreateSafeAsync(
                CacheKeys.ProjectsAll,
                async () => (await _unitOfWork.ProjectRepository.GetAllAsync()).ToList(),
                TimeSpan.FromMinutes(10));

            // Get all employees who are already managing a project (except current)
            var managingEmployeeIds = projects
                .Where(p => p.ProjectManagerId.HasValue && p.Id != currentProjectId)
                .Select(p => p.ProjectManagerId!.Value)
                .ToHashSet();

            // Filter available managers: active, not deleted, not managing another project
            var availableManagers = employees
                .Where(e => !managingEmployeeIds.Contains(e.Id))
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

        private async Task<bool> CanAccessProjectAsync(ERP.DAL.Models.Project project)
        {
            if (User.IsInRole("CEO"))
            {
                return true;
            }

            var employeeIdClaim = User.FindFirst("EmployeeId")?.Value;
            if (!int.TryParse(employeeIdClaim, out var employeeId))
            {
                return false;
            }

            if (User.IsInRole("ProjectManager"))
            {
                return project.ProjectManagerId == employeeId;
            }

            if (User.IsInRole("Employee"))
            {
                var assignedEmployees = await _unitOfWork.ProjectRepository.GetEmployeesByProjectQueryableAsync(project.Id);
                return assignedEmployees.Any(e => e.Id == employeeId);
            }

            return false;
        }

        /// <summary>
        /// Invalidation rules:
        /// - Department list cache is shared by create/edit forms.
        /// - Available managers list changes when projects or employee role assignments change.
        /// - Report caches are evicted to avoid stale aggregate values.
        /// </summary>
        private Task InvalidateProjectRelatedCachesAsync(int? projectId = null)
        {
            // Repository-level invalidation keeps project list and profile caches consistent.
            return _unitOfWork.ProjectRepository.InvalidateCacheAsync(projectId);
        }
        #endregion
    }
}
