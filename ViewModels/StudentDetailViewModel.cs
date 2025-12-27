using System;
using System.Collections.Generic;
using AttendenceManagementSystem.Models;

namespace AttendenceManagementSystem.ViewModels
{
    public class StudentDetailViewModel
    {
        // Student Information
        public int StudentId { get; set; }
        public string RollNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string BatchYear { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        
        // Course Information
        public int? CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        
        // Attendance Summary
        public int TotalClasses { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public int LateCount { get; set; }
        public double AttendancePercentage { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = string.Empty;
        
        // Date Range
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        
        // Attendance History
        public List<AttendanceHistoryItem> AttendanceHistory { get; set; } = new List<AttendanceHistoryItem>();
    }

    public class AttendanceHistoryItem
    {
        public DateTime Date { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public AttendanceStatus Status { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public string StatusColor { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public DateTime? MarkedAt { get; set; }
    }
}
