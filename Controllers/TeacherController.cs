using AttendenceManagementSystem.Data;
using AttendenceManagementSystem.Models;
using AttendenceManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AttendenceManagementSystem.Controllers
{
    [Authorize(Roles = "Teacher")]
    public class TeacherController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IAttendanceService _attendanceService;
        private readonly ITimetableService _timetableService;

        public TeacherController(ApplicationDbContext context, IAttendanceService attendanceService, ITimetableService timetableService)
        {
            _context = context;
            _attendanceService = attendanceService;
            _timetableService = timetableService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null)
                return RedirectToAction("Error", "Home");

            var assignedCourses = await _context.Timetables
                .Include(t => t.Course)
                .Where(t => t.TeacherId == teacher.Id)
                .Select(t => t.Course)
                .Distinct()
                .ToListAsync();

            var todayAttendance = await _context.Attendances
                .Include(a => a.Course)
                .Where(a => a.MarkedByUserId == GetCurrentUserId() &&
                           a.Date == DateOnly.FromDateTime(DateTime.UtcNow))
                .CountAsync();

            var stats = new
            {
                AssignedCourses = assignedCourses.Count,
                TodayAttendanceMarked = todayAttendance,
                TotalStudents = await _context.Students.CountAsync()
            };

            return View(stats);
        }

        public async Task<IActionResult> AssignedCourses()
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null)
                return RedirectToAction("Error", "Home");

            var courses = await _context.Timetables
                .Include(t => t.Course)
                .Include(t => t.Batch)
                .Include(t => t.Section)
                .Where(t => t.TeacherId == teacher.Id)
                .Select(t => new
                {
                    t.Course,
                    BatchSection = $"{t.Batch.Year} - {t.Section.Name}",
                    t.DayOfWeek,
                    t.StartTime,
                    t.EndTime
                })
                .ToListAsync();

            var groupedCourses = courses.GroupBy(c => c.Course.Id)
                .Select(g => new
                {
                    Course = g.First().Course,
                    Schedule = g.Select(x => new { x.BatchSection, x.DayOfWeek, x.StartTime, x.EndTime }).ToList()
                });

            return View(groupedCourses);
        }

        public async Task<IActionResult> MarkAttendance(int? courseId, DateOnly? date)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null)
                return RedirectToAction("Error", "Home");

            var selectedDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var selectedCourseId = courseId;

            // Get teacher's courses
            var teacherCourses = await _context.Timetables
                .Include(t => t.Course)
                .Where(t => t.TeacherId == teacher.Id)
                .Select(t => t.Course)
                .Distinct()
                .ToListAsync();

            if (!teacherCourses.Any())
            {
                ShowMessage("No courses assigned to you.", "warning");
                return View(new MarkAttendanceViewModel());
            }

            // If no course selected, use first course
            if (!selectedCourseId.HasValue)
            {
                selectedCourseId = teacherCourses.First().Id;
            }

            // Check if attendance can be edited for this date
            var canEdit = await _attendanceService.CanEditAttendanceAsync(selectedDate);
            if (!canEdit)
            {
                ShowMessage($"Attendance for {selectedDate:yyyy-MM-dd} is locked and cannot be modified.", "warning");
            }

            // Get students enrolled in the selected course for teacher's batches/sections
            var students = await GetStudentsForCourseAsync(teacher.Id, selectedCourseId.Value);

            // Get existing attendance for the date
            var existingAttendance = await _context.Attendances
                .Where(a => a.CourseId == selectedCourseId.Value &&
                           a.Date == selectedDate &&
                           students.Select(s => s.Id).Contains(a.StudentId))
                .ToDictionaryAsync(a => a.StudentId, a => a.Status);

            var viewModel = new MarkAttendanceViewModel
            {
                CourseId = selectedCourseId.Value,
                Date = selectedDate,
                CanEdit = canEdit,
                Courses = teacherCourses,
                Students = students.Select(s => new StudentAttendanceViewModel
                {
                    StudentId = s.Id,
                    RollNumber = s.RollNumber,
                    FullName = s.FullName,
                    Batch = s.Batch.Year,
                    Section = s.Section.Name,
                    Status = existingAttendance.ContainsKey(s.Id) ? existingAttendance[s.Id] : AttendanceStatus.Absent
                }).ToList()
            };

            ViewData["Courses"] = new SelectList(teacherCourses, "Id", "Code", selectedCourseId);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAttendance(MarkAttendanceViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateMarkAttendanceViewData(model.CourseId, model.Date);
                return View(model);
            }

            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null)
                return RedirectToAction("Error", "Home");

            var canEdit = await _attendanceService.CanEditAttendanceAsync(model.Date);
            if (!canEdit)
            {
                ShowMessage($"Attendance for {model.Date:yyyy-MM-dd} is locked and cannot be modified.", "error");
                await PopulateMarkAttendanceViewData(model.CourseId, model.Date);
                return View(model);
            }

            int recordsUpdated = 0;
            foreach (var student in model.Students)
            {
                var success = await _attendanceService.MarkAttendanceAsync(
                    student.StudentId,
                    model.CourseId,
                    model.Date,
                    student.Status,
                    GetCurrentUserId(),
                    AttendanceSource.Manual
                );

                if (success) recordsUpdated++;
            }

            ShowMessage($"Successfully updated attendance for {recordsUpdated} students.");
            return RedirectToAction(nameof(AttendanceHistory), new { courseId = model.CourseId });
        }

        public async Task<IActionResult> AttendanceHistory(int? courseId, DateOnly? fromDate, DateOnly? toDate)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null)
                return RedirectToAction("Error", "Home");

            var teacherCourses = await _context.Timetables
                .Include(t => t.Course)
                .Where(t => t.TeacherId == teacher.Id)
                .Select(t => t.Course)
                .Distinct()
                .ToListAsync();

            if (!teacherCourses.Any())
            {
                ShowMessage("No courses assigned to you.", "warning");
                return View(new List<Attendance>());
            }

            var selectedCourseId = courseId ?? teacherCourses.First().Id;
            var selectedFromDate = fromDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
            var selectedToDate = toDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var attendance = await _context.Attendances
                .Include(a => a.Student)
                .Include(a => a.Course)
                .Where(a => a.CourseId == selectedCourseId &&
                           a.Date >= selectedFromDate &&
                           a.Date <= selectedToDate &&
                           a.MarkedByUserId == GetCurrentUserId())
                .OrderByDescending(a => a.Date)
                .ThenBy(a => a.Student.RollNumber)
                .ToListAsync();

            ViewData["Courses"] = new SelectList(teacherCourses, "Id", "Code", selectedCourseId);
            ViewData["FromDate"] = selectedFromDate.ToString("yyyy-MM-dd");
            ViewData["ToDate"] = selectedToDate.ToString("yyyy-MM-dd");

            return View(attendance);
        }

        public async Task<IActionResult> StudentSummary(int courseId)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null)
                return RedirectToAction("Error", "Home");

            var course = await _context.Courses.FindAsync(courseId);
            if (course == null)
            {
                ShowMessage("Course not found.", "error");
                return RedirectToAction(nameof(AssignedCourses));
            }

            var students = await GetStudentsForCourseAsync(teacher.Id, courseId);

            var summary = new List<StudentAttendanceSummary>();
            foreach (var student in students)
            {
                var attendances = await _context.Attendances
                    .Where(a => a.StudentId == student.Id && a.CourseId == courseId)
                    .ToListAsync();

                var totalClasses = attendances.Count;
                var presentClasses = attendances.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late);
                var percentage = totalClasses > 0 ? Math.Round((double)presentClasses / totalClasses * 100, 2) : 0;

                summary.Add(new StudentAttendanceSummary
                {
                    StudentId = student.Id,
                    RollNumber = student.RollNumber,
                    FullName = student.FullName,
                    Batch = student.Batch.Year,
                    Section = student.Section.Name,
                    TotalClasses = totalClasses,
                    PresentClasses = presentClasses,
                    Percentage = percentage
                });
            }

            ViewData["Course"] = course;
            return View(summary.OrderByDescending(s => s.Percentage).ToList());
        }

        #region Private Methods
        private async Task<Teacher?> GetCurrentTeacherAsync()
        {
            var userId = GetCurrentUserId();
            return await _context.Teachers
                .Include(t => t.Department)
                .FirstOrDefaultAsync(t => t.UserId == userId);
        }

        private async Task<List<Student>> GetStudentsForCourseAsync(int teacherId, int courseId)
        {
            // Get batches and sections this teacher teaches this course
            var teacherSchedules = await _context.Timetables
                .Where(t => t.TeacherId == teacherId && t.CourseId == courseId)
                .Select(t => new { t.BatchId, t.SectionId })
                .Distinct()
                .ToListAsync();

            var students = new List<Student>();
            foreach (var schedule in teacherSchedules)
            {
                var batchStudents = await _context.Students
                    .Include(s => s.Batch)
                    .Include(s => s.Section)
                    .Where(s => s.BatchId == schedule.BatchId && s.SectionId == schedule.SectionId)
                    .ToListAsync();
                students.AddRange(batchStudents);
            }

            return students.Distinct().OrderBy(s => s.RollNumber).ToList();
        }

        private async Task PopulateMarkAttendanceViewData(int courseId, DateOnly date)
        {
            var teacher = await GetCurrentTeacherAsync();
            var teacherCourses = await _context.Timetables
                .Include(t => t.Course)
                .Where(t => t.TeacherId == teacher.Id)
                .Select(t => t.Course)
                .Distinct()
                .ToListAsync();

            ViewData["Courses"] = new SelectList(teacherCourses, "Id", "Code", courseId);
        }
        #endregion
    }

    #region View Models
    public class MarkAttendanceViewModel
    {
        public int CourseId { get; set; }
        public DateOnly Date { get; set; }
        public bool CanEdit { get; set; }
        public List<Course> Courses { get; set; } = new();
        public List<StudentAttendanceViewModel> Students { get; set; } = new();
    }

    public class StudentAttendanceViewModel
    {
        public int StudentId { get; set; }
        public string RollNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Batch { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public AttendanceStatus Status { get; set; }
    }

    public class StudentAttendanceSummary
    {
        public int StudentId { get; set; }
        public string RollNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Batch { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public int TotalClasses { get; set; }
        public int PresentClasses { get; set; }
        public double Percentage { get; set; }
    }
    #endregion
}