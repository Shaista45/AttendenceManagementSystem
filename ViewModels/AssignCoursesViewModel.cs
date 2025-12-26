using System.ComponentModel.DataAnnotations;

namespace AttendenceManagementSystem.ViewModels
{
    public class AssignCoursesViewModel
    {
        public int TeacherId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public string TeacherEmail { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;

        public List<CourseAssignmentDto> CourseAssignments { get; set; } = new();
        public List<ExistingAssignmentDto> ExistingAssignments { get; set; } = new();
    }

    public class CourseAssignmentDto
    {
        [Required(ErrorMessage = "Course is required")]
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Batch is required")]
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Section is required")]
        public int SectionId { get; set; }
        public string SectionName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Day is required")]
        public DayOfWeek DayOfWeek { get; set; }

        [Required(ErrorMessage = "Start time is required")]
        [Display(Name = "Start Time")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "End time is required")]
        [Display(Name = "End Time")]
        public TimeSpan EndTime { get; set; }
    }

    public class ExistingAssignmentDto
    {
        public int TimetableId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string BatchName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string DayOfWeek { get; set; } = string.Empty;
        public string TimeSlot { get; set; } = string.Empty;
    }
}
