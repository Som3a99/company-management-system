using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace ERP.PL.ViewModels.UserManagement
{
    /// <summary>
    /// ViewModel for changing a user's roles.
    /// </summary>
    public class ChangeRoleViewModel
    {
        [Required]
        public string UserId { get; set; } = null!;

        public string Email { get; set; } = null!;
        public string? EmployeeName { get; set; }

        /// <summary>Current roles assigned to the user.</summary>
        public List<string> CurrentRoles { get; set; } = new();

        /// <summary>Roles selected in the form (replaces current roles).</summary>
        [Required(ErrorMessage = "At least one role must be selected.")]
        public List<string> SelectedRoles { get; set; } = new();

        /// <summary>Available roles for the dropdown / checkbox list.</summary>
        public List<SelectListItem> AvailableRoles { get; set; } = new();
    }
}
