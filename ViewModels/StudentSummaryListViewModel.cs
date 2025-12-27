using System;
using System.Collections.Generic;

namespace AttendenceManagementSystem.ViewModels
{
    public class StudentSummaryListViewModel
    {
        public List<StudentSummaryItem> Students { get; set; } = new List<StudentSummaryItem>();
        
        // Filters
        public int? CourseId { get; set; }
        public int? BatchId { get; set; }
        public int? SectionId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        
        // For dropdowns
        public List<Models.Course> Courses { get; set; } = new List<Models.Course>();
        public List<Models.Batch> Batches { get; set; } = new List<Models.Batch>();
        public List<Models.Section> Sections { get; set; } = new List<Models.Section>();
    }

    public class StudentSummaryItem
    {
        public int StudentId { get; set; }
        public string RollNumber { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string BatchYear { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public int TotalClasses { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public double AttendancePercentage { get; set; }
        public string Status { get; set; } = string.Empty; // Good / Warning / Shortage
        public string StatusColor { get; set; } = string.Empty; // For color coding
    }
}
