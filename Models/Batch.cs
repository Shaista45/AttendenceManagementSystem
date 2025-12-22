using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendenceManagementSystem.Models
{
    public class Batch
    {
        public int Id { get; set; }

        [Required]
        [StringLength(4)]
        public string Year { get; set; } = string.Empty;

        [Required]
        public int DepartmentId { get; set; }

        [StringLength(50)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties - made nullable
        [ForeignKey("DepartmentId")]
        public virtual Department? Department { get; set; }
        public virtual ICollection<Section>? Sections { get; set; }
        public virtual ICollection<Student>? Students { get; set; }
        public virtual ICollection<Timetable>? Timetables { get; set; }
    }
}