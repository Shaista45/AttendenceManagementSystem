using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AttendenceManagementSystem.Models
{
    public class Student
    {
        public int Id { get; set; }

        [BindNever]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string RollNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public int DepartmentId { get; set; }

        [Required]
        public int BatchId { get; set; }

        [Required]
        public int SectionId { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [Phone]
        public string? PhoneNumber { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties - made nullable
        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        [ForeignKey("DepartmentId")]
        public virtual Department? Department { get; set; }

        [ForeignKey("BatchId")]
        public virtual Batch? Batch { get; set; }

        [ForeignKey("SectionId")]
        public virtual Section? Section { get; set; }

        public virtual ICollection<Enrollment>? Enrollments { get; set; }
        public virtual ICollection<Attendance>? Attendances { get; set; }
    }
}