using System.Collections.Generic;

namespace AttendenceManagementSystem.ViewModels
{
    public class DepartmentViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public List<BatchViewModel> Batches { get; set; } = new List<BatchViewModel>();
        public int TotalTeachers { get; set; }
        public int TotalCourses { get; set; }
        public int TotalStudents { get; set; }
        public bool HasIncompleteSetup { get; set; }
        public List<string> Alerts { get; set; } = new List<string>();
    }

    public class BatchViewModel
    {
        public int Id { get; set; }
        public string Year { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<SectionViewModel> Sections { get; set; } = new List<SectionViewModel>();
        public int StudentCount { get; set; }
    }

    public class SectionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int BatchId { get; set; }
        public string BatchYear { get; set; } = string.Empty;
        public List<TeacherSimpleViewModel> AssignedTeachers { get; set; } = new List<TeacherSimpleViewModel>();
        public int StudentCount { get; set; }
        public bool HasNoTeacher { get; set; }
    }

    public class TeacherSimpleViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class DepartmentReportViewModel
    {
        public string DepartmentName { get; set; } = string.Empty;
        public string DepartmentCode { get; set; } = string.Empty;
        public int TotalBatches { get; set; }
        public int TotalSections { get; set; }
        public int TotalStudents { get; set; }
        public int TotalCourses { get; set; }
        public int TotalTeachers { get; set; }
        public List<BatchReportViewModel> BatchReports { get; set; } = new List<BatchReportViewModel>();
    }

    public class BatchReportViewModel
    {
        public string Year { get; set; } = string.Empty;
        public int TotalSections { get; set; }
        public int TotalStudents { get; set; }
        public List<SectionReportViewModel> SectionReports { get; set; } = new List<SectionReportViewModel>();
    }

    public class SectionReportViewModel
    {
        public string Name { get; set; } = string.Empty;
        public int StudentCount { get; set; }
        public int TeacherCount { get; set; }
        public List<string> Teachers { get; set; } = new List<string>();
    }

    public class CreateDepartmentCompleteViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public List<CreateBatchViewModel> Batches { get; set; } = new List<CreateBatchViewModel>();
    }

    public class CreateBatchViewModel
    {
        public string Year { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<CreateSectionViewModel> Sections { get; set; } = new List<CreateSectionViewModel>();
    }

    public class CreateSectionViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string BatchYear { get; set; } = string.Empty;
        public List<int> TeacherIds { get; set; } = new List<int>();
    }
}
