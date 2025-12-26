using System.ComponentModel.DataAnnotations;

namespace AttendenceManagementSystem.ViewModels
{
    public class TeacherViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Full Name is required")]
        [StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "Employee ID")]
        public string? EmployeeId { get; set; }

        [Phone(ErrorMessage = "Invalid phone number")]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "Department is required")]
        [Display(Name = "Department")]
        public int DepartmentId { get; set; }

        [Display(Name = "Department Name")]
        public string? DepartmentName { get; set; }

        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string? Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Password and confirmation password do not match")]
        public string? ConfirmPassword { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } = "Pending";

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Approved")]
        public bool IsApproved { get; set; } = false;

        [Display(Name = "Assigned Courses")]
        public int AssignedCoursesCount { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
