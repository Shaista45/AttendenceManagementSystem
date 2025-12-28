using AttendenceManagementSystem.Models;
using System.Collections.Generic;

namespace AttendenceManagementSystem.ViewModels
{
    public class TimetableViewModel
    {
        public int DepartmentId { get; set; }
        public int BatchId { get; set; }
        public int SectionId { get; set; }
        public List<Timetable> TimetableEntries { get; set; } = new();
    }
}