namespace AttendenceManagementSystem.ViewModels
{
    public class ReportFilterViewModel
    {
        public int? DepartmentId { get; set; }
        public int? BatchId { get; set; }
        public int? SectionId { get; set; }
        public int? CourseId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
