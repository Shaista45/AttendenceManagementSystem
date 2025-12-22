using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendenceManagementSystem.Models
{
    public class Section
    {
        public int Id { get; set; }

        [Required]
        [StringLength(10)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int BatchId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties - made nullable
        [ForeignKey("BatchId")]
        public virtual Batch? Batch { get; set; }
        public virtual ICollection<Student>? Students { get; set; }
        public virtual ICollection<Timetable>? Timetables { get; set; }
    }
}