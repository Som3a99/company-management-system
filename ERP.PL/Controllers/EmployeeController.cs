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


        public EmployeeController(IMapper mapper, IUnitOfWork unitOfWork)
        {
            _mapper=mapper;
            _unitOfWork=unitOfWork;
        }

        #region Index
        [HttpGet]
        public IActionResult Index()
        {
            var employees = _unitOfWork.EmployeeRepository.GetAll();
            var employeeViewModels = _mapper.Map<IEnumerable<EmployeeViewModel>>(employees);
            return View(employeeViewModels);
        }
        #endregion

        #region Create
        [HttpGet]
        public IActionResult Create()
        {
            LoadDepartments();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(EmployeeViewModel employee)
        {
            ModelState.Remove("Department");
            ModelState.Remove("Image"); // Remove because it's optional
            ModelState.Remove("ImageUrl"); // Remove because controller sets i

            if (!ModelState.IsValid)
            {
                LoadDepartments();
                return View(employee);
            }
            var employeeMapped = _mapper.Map<Employee>(employee);
            if (employee.Image != null && employee.Image.Length > 0)
            {
                try
                {
                    employeeMapped.ImageUrl =
                        DocumentSettings.UploadImagePath(employee.Image, "images");
                }
                catch (ArgumentException ex)
                {
                    ModelState.AddModelError("Image", ex.Message);
                    LoadDepartments();
                    return View(employee);
                }
            }
            else
            {
                // Gender-based default avatar
                employeeMapped.ImageUrl =
                    DocumentSettings.GetDefaultAvatarByGender(employeeMapped.Gender);
            }

            _unitOfWork.EmployeeRepository.Add(employeeMapped);
            _unitOfWork.Complete();

            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region Edit
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var employee = _unitOfWork.EmployeeRepository.GetById(id);
            if (employee == null)
                return NotFound();
            
            LoadDepartments(employee.DepartmentId);

            var employeeViewModel = _mapper.Map<EmployeeViewModel>(employee);
            return View(employeeViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(EmployeeViewModel viewModel)
        {
            // Remove navigation properties from validation
            ModelState.Remove("Department");
            ModelState.Remove("Image"); // Optional
            ModelState.Remove("ImageUrl"); // Set by controller

            if (!ModelState.IsValid)
            {
                LoadDepartments(viewModel.DepartmentId);
                return View(viewModel);
            }

            // CRITICAL: Load existing entity from database
            var existingEmployee = _unitOfWork.EmployeeRepository.GetById(viewModel.Id);

            if (existingEmployee == null)
                return NotFound();

            // Handle image logic BEFORE mapping (manual control)
            if (viewModel.Image != null && viewModel.Image.Length > 0)
            {
                try
                {
                    // Delete old image if it exists and is not a default avatar
                    if (!DocumentSettings.IsDefaultAvatar(existingEmployee.ImageUrl))
                    {
                        DocumentSettings.DeleteImage(existingEmployee.ImageUrl, "images");
                    }

                    // Upload new image
                    existingEmployee.ImageUrl = DocumentSettings.UploadImagePath(viewModel.Image, "images");
                }
                catch (ArgumentException ex)
                {
                    ModelState.AddModelError("Image", ex.Message);
                    LoadDepartments(viewModel.DepartmentId);
                    return View(viewModel);
                }
            }
            // If gender changed and using default avatar, update to new gender's default
            else if (viewModel.Gender != existingEmployee.Gender &&
                     DocumentSettings.IsDefaultAvatar(existingEmployee.ImageUrl))
            {
                existingEmployee.ImageUrl = DocumentSettings.GetDefaultAvatarByGender(viewModel.Gender);
            }

            // Map ViewModel properties ONTO existing tracked entity
            // This preserves ImageUrl and other properties configured to be ignored
            _mapper.Map(viewModel, existingEmployee);

            // Update and save
            _unitOfWork.EmployeeRepository.Update(existingEmployee);
            _unitOfWork.Complete();

            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region Delete

        [HttpGet]
        public IActionResult Delete(int id)
        {
            var employee = _unitOfWork.EmployeeRepository.GetById(id);
            if (employee == null)
                return NotFound();

            var employeeViewModel = _mapper.Map<EmployeeViewModel>(employee);
            return View(employeeViewModel);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var employee = _unitOfWork.EmployeeRepository.GetById(id);
            if (employee == null)
                return NotFound();

            // Delete image ONLY if it's not a default avatar
            if (!string.IsNullOrEmpty(employee.ImageUrl) &&
                !DocumentSettings.IsDefaultAvatar(employee.ImageUrl))
            {
                DocumentSettings.DeleteImage(employee.ImageUrl, "images");
            }
            _unitOfWork.EmployeeRepository.Delete(id);
            _unitOfWork.Complete();
            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region Helper Method

        // Helper method to load departments for dropdown
        private void LoadDepartments(int? selectedDepartmentId = null)
        {
            var departments = _unitOfWork.DepartmentRepository.GetAll();
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
