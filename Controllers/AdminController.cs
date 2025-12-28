using System;
using System.Linq;
using System.Threading.Tasks;
using AttendenceManagementSystem.Data;
using AttendenceManagementSystem.Models;
using AttendenceManagementSystem.Services;
using AttendenceManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AttendenceManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IExcelService _excelService;
        private readonly IReportService _reportService;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(ApplicationDbContext context, IExcelService excelService, IReportService reportService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _excelService = excelService;
            _reportService = reportService;
            _userManager = userManager;
        }

        public IActionResult Dashboard()
        {
            var stats = new
            {
                TotalStudents = _context.Students.Count(),
                TotalTeachers = _context.Teachers.Count(),
                TotalCourses = _context.Courses.Count(),
                TotalDepartments = _context.Departments.Count()
            };

            return View(stats);
        }

        // ... [Existing Department, Student, Batch, Course, Teacher Regions remain unchanged] ...
        // (I have hidden them for brevity, but they are preserved in logic. 
        //  The ONLY new code added is the Region below.)

        #region Timetable Management

        public async Task<IActionResult> ManageTimetable(int? departmentId, int? batchId, int? sectionId)
        {
            // 1. Populate Filter Dropdowns
            ViewData["Departments"] = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name", departmentId);

            // Only load batches/sections if parent is selected
            if (departmentId.HasValue)
            {
                ViewData["Batches"] = new SelectList(await _context.Batches.Where(b => b.DepartmentId == departmentId).ToListAsync(), "Id", "Year", batchId);
            }
            else
            {
                ViewData["Batches"] = new SelectList(Enumerable.Empty<SelectListItem>());
            }

            if (batchId.HasValue)
            {
                ViewData["Sections"] = new SelectList(await _context.Sections.Where(s => s.BatchId == batchId).ToListAsync(), "Id", "Name", sectionId);
            }
            else
            {
                ViewData["Sections"] = new SelectList(Enumerable.Empty<SelectListItem>());
            }

            // 2. Prepare View Model
            var viewModel = new TimetableViewModel();

            // 3. Fetch Timetable if filters are applied
            if (departmentId.HasValue && batchId.HasValue && sectionId.HasValue)
            {
                var timetableEntries = await _context.Timetables
                    .Include(t => t.Course)
                    .Include(t => t.Teacher)
                    .Include(t => t.Batch)
                    .Include(t => t.Section)
                    .Where(t => t.BatchId == batchId && t.SectionId == sectionId)
                    .OrderBy(t => t.DayOfWeek)
                    .ThenBy(t => t.StartTime)
                    .ToListAsync();

                viewModel.DepartmentId = departmentId.Value;
                viewModel.BatchId = batchId.Value;
                viewModel.SectionId = sectionId.Value;
                viewModel.TimetableEntries = timetableEntries;

                // Load data for the "Add Entry" modal
                ViewData["Courses"] = new SelectList(await _context.Courses.Where(c => c.DepartmentId == departmentId).ToListAsync(), "Id", "Title");
                ViewData["Teachers"] = new SelectList(await _context.Teachers.Where(t => t.DepartmentId == departmentId && t.IsActive).ToListAsync(), "Id", "FullName");
            }

            return View("~/Views/Admin/Timetable/Index.cshtml", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTimetableEntry(int departmentId, int batchId, int sectionId, int courseId, int teacherId, DayOfWeek dayOfWeek, TimeSpan startTime, TimeSpan endTime)
        {
            try
            {
                // Basic Validation
                if (startTime >= endTime)
                {
                    ShowErrorMessage("Start time must be before end time.");
                    return RedirectToAction(nameof(ManageTimetable), new { departmentId, batchId, sectionId });
                }

                // 1. Check for Section Conflict (The section cannot have two classes at once)
                var sectionConflict = await _context.Timetables.AnyAsync(t =>
                    t.SectionId == sectionId &&
                    t.DayOfWeek == dayOfWeek &&
                    ((t.StartTime <= startTime && t.EndTime > startTime) || (t.StartTime < endTime && t.EndTime >= endTime)));

                if (sectionConflict)
                {
                    ShowErrorMessage("Conflict: This section already has a class scheduled at this time.");
                    return RedirectToAction(nameof(ManageTimetable), new { departmentId, batchId, sectionId });
                }

                // 2. Check for Teacher Conflict (The teacher cannot be in two places at once)
                var teacherConflict = await _context.Timetables.AnyAsync(t =>
                    t.TeacherId == teacherId &&
                    t.DayOfWeek == dayOfWeek &&
                    ((t.StartTime <= startTime && t.EndTime > startTime) || (t.StartTime < endTime && t.EndTime >= endTime)));

                if (teacherConflict)
                {
                    ShowErrorMessage("Conflict: The selected teacher is already teaching another class at this time.");
                    return RedirectToAction(nameof(ManageTimetable), new { departmentId, batchId, sectionId });
                }

                // Create Entry
                var timetable = new Timetable
                {
                    CourseId = courseId,
                    TeacherId = teacherId,
                    BatchId = batchId,
                    SectionId = sectionId,
                    DayOfWeek = dayOfWeek,
                    StartTime = startTime,
                    EndTime = endTime,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Timetables.Add(timetable);
                await _context.SaveChangesAsync();

                ShowMessage("Class scheduled successfully!");
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error scheduling class: {ex.Message}");
            }

            return RedirectToAction(nameof(ManageTimetable), new { departmentId, batchId, sectionId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTimetableEntry(int id, int departmentId, int batchId, int sectionId)
        {
            var entry = await _context.Timetables.FindAsync(id);
            if (entry != null)
            {
                _context.Timetables.Remove(entry);
                await _context.SaveChangesAsync();
                ShowMessage("Class removed from schedule.");
            }
            else
            {
                ShowErrorMessage("Entry not found.");
            }
            return RedirectToAction(nameof(ManageTimetable), new { departmentId, batchId, sectionId });
        }

        #endregion

        // ... [Include your existing methods: Department Management, Student Management, Reports, etc.] ...
        #region Department Management
        public async Task<IActionResult> Departments()
        {
            var departments = await _context.Departments
                .Include(d => d.Batches)
                .Include(d => d.Courses)
                .Include(d => d.Teachers)
                .ToListAsync();
            return View("~/Views/Admin/Departments/Index.cshtml", departments);
        }
        // ... (Keep all other existing methods from your uploaded file here) ...
        // ...
        
        #region Student Summary
        // ... (Keep existing Student Summary methods) ...
        #endregion

        // ... (Keep Utility Methods) ...
    }

    public class TimetableViewModel
    {
        public int DepartmentId { get; set; }
        public int BatchId { get; set; }
        public int SectionId { get; set; }
        public List<Timetable> TimetableEntries { get; set; } = new();
    }
}