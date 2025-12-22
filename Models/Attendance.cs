using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendenceManagementSystem.Models
{
    public enum AttendanceStatus
    {
        Present,
        Absent,
        Late
    }

    public enum AttendanceSource
    {
        Manual,
        Auto
    }

    public class Attendance
    {
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Required]
        public DateOnly Date { get; set; }

        [Required]
        public AttendanceStatus Status { get; set; }

        [Required]
        public string MarkedByUserId { get; set; } = string.Empty;

        public DateTime MarkedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public AttendanceSource Source { get; set; }

        public bool IsLocked { get; set; } = false;

        public string? Remarks { get; set; }

        // Navigation properties - made nullable
        [ForeignKey("StudentId")]
        public virtual Student? Student { get; set; }

        [ForeignKey("CourseId")]
        public virtual Course? Course { get; set; }

        [ForeignKey("MarkedByUserId")]
        public virtual ApplicationUser? MarkedByUser { get; set; }
    }
}