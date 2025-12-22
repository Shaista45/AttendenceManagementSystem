using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendenceManagementSystem.Models
{
    public class UploadLog
    {
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public string UploadedBy { get; set; } = string.Empty;

        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = string.Empty; // Success, Failed

        public string? ErrorMessage { get; set; }

        public int RecordsProcessed { get; set; }

        // Navigation properties - made nullable
        [ForeignKey("UploadedBy")]
        public virtual ApplicationUser? UploadedByUser { get; set; }
    }
}