using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendenceManagementSystem.Models
{
    public class Teacher
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public int DepartmentId { get; set; }

        [StringLength(50)]
        public string? EmployeeId { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [Phone]
        public string? PhoneNumber { get; set; }

        public bool IsApproved { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties - made nullable
        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        [ForeignKey("DepartmentId")]
        public virtual Department? Department { get; set; }

        public virtual ICollection<Timetable>? Timetables { get; set; }
        public virtual ICollection<Attendance>? Attendances { get; set; }
    }
}