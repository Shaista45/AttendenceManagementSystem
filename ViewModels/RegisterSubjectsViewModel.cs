using Microsoft.AspNetCore.Mvc.Rendering;

namespace AttendenceManagementSystem.ViewModels
{
    public class RegisterSubjectsViewModel
    {
        public string? SelectedSemester { get; set; }
        public List<string> Semesters { get; set; } = new();
        
        public int? SelectedSectionId { get; set; }
        public List<SelectListItem> Sections { get; set; } = new();
        
        public List<CourseRegistrationItem> AvailableCourses { get; set; } = new();
        
        public int? StudentId { get; set; }
        public bool AllCoursesRegistered => AvailableCourses.Any() && AvailableCourses.All(c => c.IsRegistered);
    }

    public class CourseRegistrationItem
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public bool IsRegistered { get; set; }
    }
}