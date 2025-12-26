namespace AttendenceManagementSystem.ViewModels
{
    public class StudentAttendanceReportViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string RollNumber { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Batch { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public int TotalClasses { get; set; }
        public int ClassesAttended { get; set; }
        public int ClassesAbsent { get; set; }
        public decimal AttendancePercentage { get; set; }
        public bool IsBelowThreshold => AttendancePercentage < 75;
    }

    public class StudentAttendanceReportPageViewModel
    {
        public ReportFilterViewModel Filters { get; set; } = new();
        public List<StudentAttendanceReportViewModel> Students { get; set; } = new();
    }
}
