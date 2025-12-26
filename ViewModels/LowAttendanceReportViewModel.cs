namespace AttendenceManagementSystem.ViewModels
{
    public class LowAttendanceReportViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string RollNumber { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Batch { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public int TotalClasses { get; set; }
        public int ClassesAttended { get; set; }
        public decimal AttendancePercentage { get; set; }
        public int ShortfallClasses { get; set; }
    }

    public class LowAttendanceReportPageViewModel
    {
        public ReportFilterViewModel Filters { get; set; } = new();
        public List<LowAttendanceReportViewModel> Defaulters { get; set; } = new();
        public decimal ThresholdPercentage { get; set; } = 75;
    }
}
