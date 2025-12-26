namespace AttendenceManagementSystem.ViewModels
{
    public class TeacherReportViewModel
    {
        public int TeacherId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public int AssignedCourses { get; set; }
        public int TotalClassesTaken { get; set; }
        public int AttendanceMarked { get; set; }
        public int AttendancePending { get; set; }
        public decimal MarkingPercentage { get; set; }
    }

    public class TeacherReportPageViewModel
    {
        public ReportFilterViewModel Filters { get; set; } = new();
        public List<TeacherReportViewModel> Teachers { get; set; } = new();
    }
}
