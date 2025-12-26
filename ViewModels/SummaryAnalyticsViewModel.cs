namespace AttendenceManagementSystem.ViewModels
{
    public class SummaryAnalyticsViewModel
    {
        // Overall Statistics
        public int TotalStudents { get; set; }
        public int TotalTeachers { get; set; }
        public int TotalCourses { get; set; }
        public int TotalDepartments { get; set; }

        // Attendance Statistics
        public decimal OverallAttendancePercentage { get; set; }
        public int TotalClassesConducted { get; set; }
        public int TotalAttendanceMarked { get; set; }
        public int PendingAttendance { get; set; }

        // Student Performance
        public int StudentsAbove75Percent { get; set; }
        public int StudentsBelow75Percent { get; set; }
        public int StudentsBelow50Percent { get; set; }

        // Department-wise Statistics
        public List<DepartmentStatistic> DepartmentStats { get; set; } = new();

        // Recent Activity
        public DateTime? LastAttendanceDate { get; set; }
        public int TodayClasses { get; set; }
    }

    public class DepartmentStatistic
    {
        public string DepartmentName { get; set; } = string.Empty;
        public int StudentCount { get; set; }
        public decimal AverageAttendance { get; set; }
    }
}
