using AttendenceManagementSystem.Data;
using AttendenceManagementSystem.Models;
using AttendenceManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AttendenceManagementSystem.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IAttendanceService _attendanceService;
        private readonly ITimetableService _timetableService;
        private readonly IReportService _reportService;

        public StudentController(ApplicationDbContext context, IAttendanceService attendanceService, ITimetableService timetableService, IReportService reportService)
        {
            _context = context;
            _attendanceService = attendanceService;
            _timetableService = timetableService;
            _reportService = reportService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return RedirectToAction("Error", "Home");

            var attendancePercentage = await _attendanceService.GetStudentAttendancePercentageAsync(student.Id);
            var currentClass = await _timetableService.GetCurrentClassAsync(student.Id);
            var todayAttendance = await _context.Attendances
                .Include(a => a.Course)
                .Where(a => a.StudentId == student.Id && a.Date == DateOnly.FromDateTime(DateTime.UtcNow))
                .ToListAsync();

            var stats = new
            {
                TotalCourses = attendancePercentage.Count,
                CurrentClass = currentClass?.Course?.Title,
                TodayPresent = todayAttendance.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late),
                OverallPercentage = attendancePercentage.Any() ? attendancePercentage.Values.Average() : 0
            };

            ViewData["Student"] = student;
            ViewData["CurrentClass"] = currentClass;
            ViewData["AttendancePercentage"] = attendancePercentage;
            ViewData["TodayAttendance"] = todayAttendance;

            return View(stats);
        }

        public async Task<IActionResult> MyAttendance(int? courseId, DateOnly? fromDate, DateOnly? toDate)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return RedirectToAction("Error", "Home");

            var enrolledCourses = await _context.Enrollments
                .Include(e => e.Course)
                .Where(e => e.StudentId == student.Id)
                .Select(e => e.Course)
                .ToListAsync();

            if (!enrolledCourses.Any())
            {
                ShowMessage("You are not enrolled in any courses.", "warning");
                return View(new List<Attendance>());
            }

            var selectedCourseId = courseId ?? enrolledCourses.First().Id;
            var selectedFromDate = fromDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
            var selectedToDate = toDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var attendance = await _attendanceService.GetStudentAttendanceAsync(
                student.Id, selectedCourseId, selectedFromDate, selectedToDate);

            ViewData["Courses"] = new SelectList(enrolledCourses, "Id", "Code", selectedCourseId);
            ViewData["FromDate"] = selectedFromDate.ToString("yyyy-MM-dd");
            ViewData["ToDate"] = selectedToDate.ToString("yyyy-MM-dd");
            ViewData["Student"] = student;

            return View(attendance);
        }

        public async Task<IActionResult> AttendanceSummary()
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return RedirectToAction("Error", "Home");

            var attendancePercentage = await _attendanceService.GetStudentAttendancePercentageAsync(student.Id);
            var summary = new List<CourseAttendanceSummary>();

            foreach (var (courseId, percentage) in attendancePercentage)
            {
                var course = await _context.Courses.FindAsync(courseId);
                var attendances = await _context.Attendances
                    .Where(a => a.StudentId == student.Id && a.CourseId == courseId)
                    .ToListAsync();

                var totalClasses = attendances.Count;
                var presentClasses = attendances.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late);
                var absentClasses = totalClasses - presentClasses;

                summary.Add(new CourseAttendanceSummary
                {
                    CourseId = courseId,
                    CourseCode = course?.Code ?? "Unknown",
                    CourseTitle = course?.Title ?? "Unknown",
                    TotalClasses = totalClasses,
                    PresentClasses = presentClasses,
                    AbsentClasses = absentClasses,
                    Percentage = percentage
                });
            }

            ViewData["Student"] = student;
            return View(summary.OrderByDescending(s => s.Percentage).ToList());
        }

        public async Task<IActionResult> MyTimetable()
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return RedirectToAction("Error", "Home");

            var timetable = await _timetableService.GetStudentTimetableAsync(student.Id);

            // Group by day of week
            var groupedTimetable = timetable
                .GroupBy(t => t.DayOfWeek)
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.StartTime).ToList());

            ViewData["Student"] = student;
            return View(groupedTimetable);
        }

        public async Task<IActionResult> DownloadReport(DateOnly? fromDate, DateOnly? toDate)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return RedirectToAction("Error", "Home");

            try
            {
                var pdfBytes = await _reportService.GenerateStudentAttendanceReportAsync(
                    student.Id, fromDate, toDate);

                var fileName = $"Attendance_Report_{student.RollNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                ShowMessage($"Error generating report: {ex.Message}", "error");
                return RedirectToAction(nameof(Dashboard));
            }
        }

        public async Task<IActionResult> AutoMark()
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return RedirectToAction("Error", "Home");

            var currentClass = await _timetableService.GetCurrentClassAsync(student.Id);
            if (currentClass == null)
            {
                ShowMessage("No ongoing class found for auto-marking attendance.", "warning");
                return RedirectToAction(nameof(Dashboard));
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var success = await _attendanceService.MarkAttendanceAsync(
                student.Id,
                currentClass.CourseId,
                today,
                AttendanceStatus.Present,
                GetCurrentUserId(),
                AttendanceSource.Auto
            );

            if (success)
            {
                ShowMessage($"Attendance auto-marked as Present for {currentClass.Course.Code}");
            }
            else
            {
                ShowMessage("Failed to auto-mark attendance. It may already be marked or locked.", "error");
            }

            return RedirectToAction(nameof(Dashboard));
        }

        #region Private Methods
        private async Task<Student?> GetCurrentStudentAsync()
        {
            var userId = GetCurrentUserId();
            return await _context.Students
                .Include(s => s.Department)
                .Include(s => s.Batch)
                .Include(s => s.Section)
                .FirstOrDefaultAsync(s => s.UserId == userId);
        }
        #endregion
    }

    #region View Models

    public class CourseAttendanceSummary
    {
        public int CourseId { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public int TotalClasses { get; set; }
        public int PresentClasses { get; set; }
        public int AbsentClasses { get; set; }
        public double Percentage { get; set; }
    }
    #endregion

}