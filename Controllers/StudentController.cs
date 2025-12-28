using AttendenceManagementSystem.Data;
using AttendenceManagementSystem.Models;
using AttendenceManagementSystem.Services;
using AttendenceManagementSystem.ViewModels; // Added this
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

        // GET: Register Subjects
        public async Task<IActionResult> RegisterSubjects(string? semester = null, int? sectionId = null)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return RedirectToAction("Error", "Home");

            // 1. Get Semesters
            var semesters = await _context.Courses
                .Where(c => !string.IsNullOrEmpty(c.Semester))
                .Select(c => c.Semester)
                .Distinct()
                .OrderByDescending(s => s)
                .ToListAsync();

            if (!semesters.Any()) 
            {
                semesters = new List<string> { "FALL 2025", "SPRING 2026" }; 
            }

            // 2. Get Sections
            var sections = await _context.Sections
                .Where(s => s.BatchId == student.BatchId)
                .Select(s => new SelectListItem 
                { 
                    Value = s.Id.ToString(), 
                    Text = s.Name,
                    Selected = s.Id == sectionId
                })
                .ToListAsync();

            var vm = new RegisterSubjectsViewModel
            {
                SelectedSemester = semester,
                Semesters = semesters,
                SelectedSectionId = sectionId,
                Sections = sections,
                StudentId = student.Id
            };

            // 3. Fetch courses if filtered
            if (!string.IsNullOrEmpty(semester) && sectionId.HasValue)
            {
                var courses = await _context.Courses
                    .Where(c => c.DepartmentId == student.DepartmentId && c.Semester == semester)
                    .ToListAsync();

                var enrollments = await _context.Enrollments
                    .Where(e => e.StudentId == student.Id)
                    .ToListAsync();

                foreach (var course in courses)
                {
                    var timetableEntry = await _context.Timetables
                        .Include(t => t.Teacher)
                        .FirstOrDefaultAsync(t => t.CourseId == course.Id && t.SectionId == sectionId.Value);

                    vm.AvailableCourses.Add(new CourseRegistrationItem
                    {
                        CourseId = course.Id,
                        CourseCode = course.Code,
                        CourseName = course.Title,
                        TeacherName = timetableEntry?.Teacher?.FullName ?? "Not Assigned",
                        IsRegistered = enrollments.Any(e => e.CourseId == course.Id)
                    });
                }
            }

            return View(vm);
        }

        // POST: Register for a specific Course
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterCourse(int courseId, string semester, int sectionId)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null) return RedirectToAction("Error", "Home");

            var exists = await _context.Enrollments.AnyAsync(e => e.StudentId == student.Id && e.CourseId == courseId);
            if (!exists)
            {
                _context.Enrollments.Add(new Enrollment
                {
                    StudentId = student.Id,
                    CourseId = courseId,
                    EnrolledAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                ShowMessage("Successfully registered for the course.");
            }
            else
            {
                ShowMessage("You are already registered for this course.", "warning");
            }

            return RedirectToAction(nameof(RegisterSubjects), new { semester, sectionId });
        }

        // POST: Unregister from a Course
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnregisterCourse(int courseId, string semester, int sectionId)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null) return RedirectToAction("Error", "Home");

            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.StudentId == student.Id && e.CourseId == courseId);

            if (enrollment != null)
            {
                _context.Enrollments.Remove(enrollment);
                await _context.SaveChangesAsync();
                ShowMessage("Successfully unregistered from the course.");
            }

            return RedirectToAction(nameof(RegisterSubjects), new { semester, sectionId });
        }

        // GET: Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return RedirectToAction("Error", "Home");

            var attendancePercentage = await _attendanceService.GetStudentAttendancePercentageAsync(student.Id);
            var currentClass = await _timetableService.GetCurrentClassAsync(student.Id);
            
            // Fetch today's schedule
            var today = DateTime.Today.DayOfWeek;
            var todaySchedule = await _context.Timetables
                .Include(t => t.Course)
                .Include(t => t.Teacher)
                .Where(t => t.BatchId == student.BatchId 
                         && t.SectionId == student.SectionId 
                         && t.DayOfWeek == today)
                .OrderBy(t => t.StartTime)
                .ToListAsync();

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
            ViewData["TodaySchedule"] = todaySchedule;

            return View(stats);
        }

        // GET: Attendance Summary (Detailed)
        public async Task<IActionResult> AttendanceSummary()
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return RedirectToAction("Error", "Home");

            // 1. Fetch all enrollments with course details
            var enrollments = await _context.Enrollments
                .Include(e => e.Course)
                .Where(e => e.StudentId == student.Id)
                .ToListAsync();

            var vm = new StudentComprehensiveSummaryViewModel
            {
                StudentName = student.FullName,
                RollNumber = student.RollNumber,
                Department = student.Department?.Name ?? "",
                Batch = student.Batch?.Year ?? "",
                Section = student.Section?.Name ?? "",
                TotalCourses = enrollments.Count
            };

            int grandTotalClasses = 0;
            int grandTotalPresent = 0;

            foreach (var enrollment in enrollments)
            {
                // Fetch attendance for this specific course
                var attendances = await _context.Attendances
                    .Where(a => a.StudentId == student.Id && a.CourseId == enrollment.CourseId)
                    .ToListAsync();

                // Get Teacher for this course (via Timetable)
                var teacherEntry = await _context.Timetables
                    .Include(t => t.Teacher)
                    .FirstOrDefaultAsync(t => t.CourseId == enrollment.CourseId && t.SectionId == student.SectionId);

                int total = attendances.Count;
                int present = attendances.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late);
                int absent = total - present;
                double pct = total > 0 ? (double)present / total * 100 : 0;

                // Logic for Status and Recovery
                string status = "Good";
                string color = "success";
                int recovery = 0;

                if (pct < 75)
                {
                    status = "Shortage";
                    color = "danger";
                    // Recovery formula: (Present + x) / (Total + x) = 0.75
                    if(total > 0)
                    {
                        recovery = (int)Math.Ceiling((0.75 * total - present) / 0.25);
                        if (recovery < 0) recovery = 0;
                    }
                }
                else if (pct < 80)
                {
                    status = "Warning";
                    color = "warning";
                }

                vm.CourseSummaries.Add(new DetailedCourseSummary
                {
                    CourseCode = enrollment.Course.Code,
                    CourseTitle = enrollment.Course.Title,
                    TeacherName = teacherEntry?.Teacher?.FullName ?? "N/A",
                    TotalClasses = total,
                    Present = present,
                    Absent = absent,
                    Percentage = Math.Round(pct, 1),
                    Status = status,
                    StatusColor = color,
                    ClassesToRecover = recovery
                });

                grandTotalClasses += total;
                grandTotalPresent += present;
            }

            vm.TotalClassesConducted = grandTotalClasses;
            vm.TotalClassesAttended = grandTotalPresent;
            vm.OverallPercentage = grandTotalClasses > 0 
                ? Math.Round((double)grandTotalPresent / grandTotalClasses * 100, 1) 
                : 0;

            return View(vm);
        }

        // GET: My Attendance (Calendar View)
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

        // GET: Timetable
        public async Task<IActionResult> MyTimetable()
        {
            var student = await GetCurrentStudentAsync();
            if (student == null)
                return RedirectToAction("Error", "Home");

            var timetable = await _timetableService.GetStudentTimetableAsync(student.Id);

            var groupedTimetable = timetable
                .GroupBy(t => t.DayOfWeek)
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.StartTime).ToList());

            ViewData["Student"] = student;
            return View(groupedTimetable);
        }

        // GET: AutoMark
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
}
    