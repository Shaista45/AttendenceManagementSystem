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

        public async Task<IActionResult> MarkAttendance(int? batchId, int? sectionId, int? courseId, DateOnly? date)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null)
                return RedirectToAction("Error", "Home");

            var selectedDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);

            // Get teacher's batches (sessions)
            var teacherBatches = await _context.Timetables
                .Include(t => t.Batch)
                .Where(t => t.TeacherId == teacher.Id)
                .Select(t => t.Batch)
                .Distinct()
                .OrderByDescending(b => b.Year)
                .ToListAsync();

            if (!teacherBatches.Any())
            {
                ShowMessage("No batches assigned to you.", "warning");
                return View(new MarkAttendanceViewModel());
            }

            // Select batch
            var selectedBatchId = batchId ?? teacherBatches.First().Id;

            // Get sections for the selected batch
            var teacherSections = await _context.Timetables
                .Include(t => t.Section)
                .Where(t => t.TeacherId == teacher.Id && t.BatchId == selectedBatchId)
                .Select(t => t.Section)
                .Distinct()
                .OrderBy(s => s.Name)
                .ToListAsync();

            // Select section
            var selectedSectionId = sectionId ?? (teacherSections.Any() ? teacherSections.First().Id : (int?)null);

            // Get courses for the selected batch and section
            var teacherCourses = new List<Course>();
            if (selectedSectionId.HasValue)
            {
                teacherCourses = await _context.Timetables
                    .Include(t => t.Course)
                    .Where(t => t.TeacherId == teacher.Id && 
                               t.BatchId == selectedBatchId && 
                               t.SectionId == selectedSectionId)
                    .Select(t => t.Course)
                    .Distinct()
                    .ToListAsync();
            }

            // Select course
            var selectedCourseId = courseId ?? (teacherCourses.Any() ? teacherCourses.First().Id : (int?)null);

            // Check if attendance can be edited for this date
            var canEdit = await _attendanceService.CanEditAttendanceAsync(selectedDate);
            if (!canEdit)
            {
                ShowMessage($"Attendance for {selectedDate:yyyy-MM-dd} is locked and cannot be modified.", "warning");
            }

            // Get students for the selected batch, section, and course
            var students = new List<Student>();
            if (selectedBatchId > 0 && selectedSectionId.HasValue && selectedCourseId.HasValue)
            {
                students = await _context.Students
                    .Include(s => s.Batch)
                    .Include(s => s.Section)
                    .Where(s => s.BatchId == selectedBatchId && s.SectionId == selectedSectionId)
                    .OrderBy(s => s.RollNumber)
                    .ToListAsync();
            }

            // Extract student IDs for query
            var studentIds = students.Select(s => s.Id).ToList();

            // Get existing attendance for the date
            var existingAttendance = new Dictionary<int, AttendanceStatus>();
            if (selectedCourseId.HasValue && studentIds.Any())
            {
                var allAttendanceForDate = await _context.Attendances
                    .Where(a => a.CourseId == selectedCourseId.Value && a.Date == selectedDate)
                    .ToListAsync();
                
                existingAttendance = allAttendanceForDate
                    .Where(a => studentIds.Contains(a.StudentId))
                    .ToDictionary(a => a.StudentId, a => a.Status);
            }

            var viewModel = new MarkAttendanceViewModel
            {
                BatchId = selectedBatchId,
                SectionId = selectedSectionId,
                CourseId = selectedCourseId ?? 0,
                Date = selectedDate,
                CanEdit = canEdit,
                Batches = teacherBatches,
                Sections = teacherSections,
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

            ViewData["Batches"] = new SelectList(teacherBatches, "Id", "Year", selectedBatchId);
            ViewData["Sections"] = new SelectList(teacherSections, "Id", "Name", selectedSectionId);
            ViewData["Courses"] = new SelectList(teacherCourses, "Id", "Code", selectedCourseId);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAttendance(MarkAttendanceViewModel model)
        {
            if (!ModelState.IsValid || model.CourseId == 0)
            {
                ShowMessage("Please select session, section, and course.", "error");
                return RedirectToAction(nameof(MarkAttendance), new { batchId = model.BatchId, sectionId = model.SectionId, courseId = model.CourseId, date = model.Date });
            }

            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null)
                return RedirectToAction("Error", "Home");

            var canEdit = await _attendanceService.CanEditAttendanceAsync(model.Date);
            if (!canEdit)
            {
                ShowMessage($"Attendance for {model.Date:yyyy-MM-dd} is locked and cannot be modified.", "error");
                return RedirectToAction(nameof(MarkAttendance), new { batchId = model.BatchId, sectionId = model.SectionId, courseId = model.CourseId, date = model.Date });
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
            return RedirectToAction(nameof(MarkAttendance), new { batchId = model.BatchId, sectionId = model.SectionId, courseId = model.CourseId, date = model.Date });
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

        #region View Models
        public class MarkAttendanceViewModel
        {
            public int BatchId { get; set; }
            public int? SectionId { get; set; }
            public int CourseId { get; set; }
            public DateOnly Date { get; set; }
            public bool CanEdit { get; set; }
            public List<Batch> Batches { get; set; } = new();
            public List<Section> Sections { get; set; } = new();
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

    #region Student Summary

    public async Task<IActionResult> StudentSummary(int? courseId, int? batchId, int? sectionId, DateTime? fromDate, DateTime? toDate)
    {
        var teacher = await GetCurrentTeacherAsync();
        if (teacher == null)
            return RedirectToAction("Error", "Home");

        var viewModel = new ViewModels.StudentSummaryListViewModel
        {
            FromDate = fromDate ?? DateTime.Now.AddMonths(-1),
            ToDate = toDate ?? DateTime.Now,
            CourseId = courseId,
            BatchId = batchId,
            SectionId = sectionId
        };

        // Load only teacher's assigned courses
        viewModel.Courses = await _context.Timetables
            .Where(t => t.TeacherId == teacher.Id)
            .Select(t => t.Course!)
            .Distinct()
            .OrderBy(c => c.Title)
            .ToListAsync();

        // Load batches where teacher has courses
        viewModel.Batches = await _context.Timetables
            .Where(t => t.TeacherId == teacher.Id)
            .Select(t => t.Batch!)
            .Distinct()
            .OrderBy(b => b.Year)
            .ToListAsync();

        if (batchId.HasValue)
        {
            // Load sections where teacher has courses for the selected batch
            viewModel.Sections = await _context.Timetables
                .Where(t => t.TeacherId == teacher.Id && t.BatchId == batchId.Value)
                .Select(t => t.Section!)
                .Distinct()
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        // If filters are applied, get student data
        if (courseId.HasValue && batchId.HasValue && sectionId.HasValue)
        {
            // Verify teacher has access to this course, batch, and section
            var hasAccess = await _context.Timetables
                .AnyAsync(t => t.TeacherId == teacher.Id 
                              && t.CourseId == courseId.Value 
                              && t.BatchId == batchId.Value 
                              && t.SectionId == sectionId.Value);

            if (!hasAccess)
            {
                ShowErrorMessage("You do not have access to this course, batch, and section combination.");
                return View(viewModel);
            }

            var students = await _context.Students
                .Where(s => s.BatchId == batchId.Value && s.SectionId == sectionId.Value)
                .Include(s => s.Batch)
                .Include(s => s.Section)
                .ToListAsync();

            foreach (var student in students)
            {
                // Get attendance records for the student in the selected course and date range
                var fromDateOnly = DateOnly.FromDateTime(viewModel.FromDate);
                var toDateOnly = DateOnly.FromDateTime(viewModel.ToDate);
                
                var attendanceRecords = await _context.Attendances
                    .Where(a => a.StudentId == student.Id 
                                && a.CourseId == courseId.Value 
                                && a.Date >= fromDateOnly 
                                && a.Date <= toDateOnly)
                    .ToListAsync();

                var totalClasses = attendanceRecords.Count;
                var presentCount = attendanceRecords.Count(a => a.Status == AttendanceStatus.Present);
                var absentCount = attendanceRecords.Count(a => a.Status == AttendanceStatus.Absent);
                var percentage = totalClasses > 0 ? (double)presentCount / totalClasses * 100 : 0;

                string status;
                string statusColor;

                if (percentage >= 75)
                {
                    status = "Good";
                    statusColor = "success";
                }
                else if (percentage >= 65)
                {
                    status = "Warning";
                    statusColor = "warning";
                }
                else
                {
                    status = "Shortage";
                    statusColor = "danger";
                }

                viewModel.Students.Add(new ViewModels.StudentSummaryItem
                {
                    StudentId = student.Id,
                    RollNumber = student.RollNumber,
                    StudentName = student.FullName,
                    BatchYear = student.Batch?.Year ?? "",
                    SectionName = student.Section?.Name ?? "",
                    TotalClasses = totalClasses,
                    PresentCount = presentCount,
                    AbsentCount = absentCount,
                    AttendancePercentage = Math.Round(percentage, 2),
                    Status = status,
                    StatusColor = statusColor
                });
            }
        }

        return View(viewModel);
    }

    public async Task<IActionResult> StudentDetail(int id, int? courseId, DateTime? fromDate, DateTime? toDate)
    {
        var teacher = await GetCurrentTeacherAsync();
        if (teacher == null)
            return RedirectToAction("Error", "Home");

        var student = await _context.Students
            .Include(s => s.Batch)
            .Include(s => s.Section)
            .Include(s => s.Department)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
        {
            ShowErrorMessage("Student not found.");
            return RedirectToAction(nameof(StudentSummary));
        }

        var viewModel = new ViewModels.StudentDetailViewModel
        {
            StudentId = student.Id,
            RollNumber = student.RollNumber,
            FullName = student.FullName,
            Email = student.Email ?? "",
            PhoneNumber = student.PhoneNumber ?? "",
            BatchYear = student.Batch?.Year ?? "",
            SectionName = student.Section?.Name ?? "",
            DepartmentName = student.Department?.Name ?? "",
            FromDate = fromDate ?? DateTime.Now.AddMonths(-1),
            ToDate = toDate ?? DateTime.Now,
            CourseId = courseId
        };

        // Get course info if courseId is provided
        if (courseId.HasValue)
        {
            // Verify teacher has access to this course
            var hasAccess = await _context.Timetables
                .AnyAsync(t => t.TeacherId == teacher.Id && t.CourseId == courseId.Value);

            if (!hasAccess)
            {
                ShowErrorMessage("You do not have access to this course.");
                return RedirectToAction(nameof(StudentSummary));
            }

            var course = await _context.Courses.FindAsync(courseId.Value);
            if (course != null)
            {
                viewModel.CourseName = course.Title;
                viewModel.CourseCode = course.Code;
            }

            // Get attendance records
            var fromDateOnly = DateOnly.FromDateTime(viewModel.FromDate);
            var toDateOnly = DateOnly.FromDateTime(viewModel.ToDate);
            
            var attendanceRecords = await _context.Attendances
                .Where(a => a.StudentId == id 
                            && a.CourseId == courseId.Value 
                            && a.Date >= fromDateOnly 
                            && a.Date <= toDateOnly)
                .Include(a => a.Course)
                .OrderByDescending(a => a.Date)
                .ToListAsync();

            viewModel.TotalClasses = attendanceRecords.Count;
            viewModel.PresentCount = attendanceRecords.Count(a => a.Status == AttendanceStatus.Present);
            viewModel.AbsentCount = attendanceRecords.Count(a => a.Status == AttendanceStatus.Absent);
            viewModel.LateCount = attendanceRecords.Count(a => a.Status == AttendanceStatus.Late);
            
            if (viewModel.TotalClasses > 0)
            {
                viewModel.AttendancePercentage = Math.Round((double)viewModel.PresentCount / viewModel.TotalClasses * 100, 2);
            }

            if (viewModel.AttendancePercentage >= 75)
            {
                viewModel.Status = "Good";
                viewModel.StatusColor = "success";
            }
            else if (viewModel.AttendancePercentage >= 65)
            {
                viewModel.Status = "Warning";
                viewModel.StatusColor = "warning";
            }
            else
            {
                viewModel.Status = "Shortage";
                viewModel.StatusColor = "danger";
            }

            // Build attendance history
            foreach (var record in attendanceRecords)
            {
                string statusText = record.Status.ToString();
                string statusColor = record.Status switch
                {
                    AttendanceStatus.Present => "success",
                    AttendanceStatus.Absent => "danger",
                    AttendanceStatus.Late => "warning",
                    _ => "secondary"
                };

                viewModel.AttendanceHistory.Add(new ViewModels.AttendanceHistoryItem
                {
                    Date = record.Date.ToDateTime(TimeOnly.MinValue),
                    CourseName = record.Course?.Title ?? "",
                    CourseCode = record.Course?.Code ?? "",
                    Status = record.Status,
                    StatusText = statusText,
                    StatusColor = statusColor,
                    Remarks = record.Remarks ?? "",
                    MarkedAt = record.MarkedAt
                });
            }
        }

        return View(viewModel);
    }

    #endregion
    }
}