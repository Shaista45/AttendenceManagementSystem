namespace AttendenceManagementSystem.ViewModels
{
    public class CourseReportViewModel
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public int TotalLectures { get; set; }
        public int TotalStudents { get; set; }
        public decimal AverageAttendance { get; set; }
        public int NumberOfDefaulters { get; set; }
    }

    public class CourseReportPageViewModel
    {
        public ReportFilterViewModel Filters { get; set; } = new();
        public List<CourseReportViewModel> Courses { get; set; } = new();
    }
}
