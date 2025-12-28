using System.Collections.Generic;

namespace AttendenceManagementSystem.ViewModels
{
    public class StudentComprehensiveSummaryViewModel
    {
        public string StudentName { get; set; } = string.Empty;
        public string RollNumber { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Batch { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        
        public int TotalCourses { get; set; }
        public double OverallPercentage { get; set; }
        public int TotalClassesConducted { get; set; }
        public int TotalClassesAttended { get; set; }
        
        public List<DetailedCourseSummary> CourseSummaries { get; set; } = new List<DetailedCourseSummary>();
    }

    public class DetailedCourseSummary
    {
        public string CourseCode { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public int TotalClasses { get; set; }
        public int Present { get; set; }
        public int Absent { get; set; }
        public int Late { get; set; }
        public double Percentage { get; set; }
        
        // Helper properties for UI
        public string Status { get; set; } = "Good";
        public string StatusColor { get; set; } = "success";
        public int ClassesToRecover { get; set; }
    }
}