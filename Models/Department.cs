using System.ComponentModel.DataAnnotations;

namespace AttendenceManagementSystem.Models
{
    public class Department
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(10)]
        public string? Code { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties - made nullable
        public virtual ICollection<Batch>? Batches { get; set; }
        public virtual ICollection<Course>? Courses { get; set; }
        public virtual ICollection<Teacher>? Teachers { get; set; }
        public virtual ICollection<Student>? Students { get; set; }
    }
}