namespace AttendenceManagementSystem.ViewModels
{
    public class DailyAttendanceReportViewModel
    {
        public DateTime Date { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public int TotalStudents { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public decimal AttendancePercentage { get; set; }
    }

    public class DailyAttendanceReportPageViewModel
    {
        public ReportFilterViewModel Filters { get; set; } = new();
        public List<DailyAttendanceReportViewModel> DailyRecords { get; set; } = new();
    }
}
