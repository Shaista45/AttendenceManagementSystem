using System;
using System.Linq;
using System.Threading.Tasks;
using AttendenceManagementSystem.Data;
using AttendenceManagementSystem.Models;
using AttendenceManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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

        public AdminController(ApplicationDbContext context, IExcelService excelService, IReportService reportService)
        {
            _context = context;
            _excelService = excelService;
            _reportService = reportService;
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

        #region Department Management
        public async Task<IActionResult> Departments()
        {
            var departments = await _context.Departments
                .Include(d => d.Batches)
                .Include(d => d.Courses)
                .Include(d => d.Teachers)
                .ToListAsync();
            // Views are organized under Views/Admin/Departments/Index.cshtml
            // Return the explicit path so the view engine finds the Index inside the Departments subfolder
            return View("~/Views/Admin/Departments/Index.cshtml", departments);
        }

        public IActionResult CreateDepartment()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDepartment(Department department)
        {
            if (ModelState.IsValid)
            {
                department.CreatedAt = DateTime.UtcNow;
                // Use the DbSet to add the entity so the correct EF Core Add method is used
                _context.Departments.Add(department);
                await _context.SaveChangesAsync();
                ShowMessage("Department created successfully!");
                return RedirectToAction(nameof(Departments));
            }
            return View(department);
        }

        public async Task<IActionResult> EditDepartment(int? id)
        {
            if (id == null)
                return NotFound();

            var department = await _context.Departments.FindAsync(id);
            if (department == null)
                return NotFound();

            return View(department);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDepartment(int id, Department department)
        {
            if (id != department.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(department);
                    await _context.SaveChangesAsync();
                    ShowMessage("Department updated successfully!");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DepartmentExists(department.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Departments));
            }
            return View(department);
        }

        public async Task<IActionResult> ViewDepartment(int? id)
        {
            if (id == null)
                return NotFound();

            var department = await _context.Departments
                .Include(d => d.Batches)
                .Include(d => d.Courses)
                .Include(d => d.Teachers)
                .Include(d => d.Students)
                .FirstOrDefaultAsync(m => m.Id == id);
            
            if (department == null)
                return NotFound();

            return View("~/Views/Admin/Departments/ViewDepartment.cshtml", department);
        }

        public async Task<IActionResult> DeleteDepartment(int? id)
        {
            if (id == null)
                return NotFound();

            var department = await _context.Departments
                .FirstOrDefaultAsync(m => m.Id == id);
            if (department == null)
                return NotFound();

            return View(department);
        }

        [HttpPost, ActionName("DeleteDepartment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDepartmentConfirmed(int id)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department != null)
            {
                _context.Departments.Remove(department);
                await _context.SaveChangesAsync();
                ShowMessage("Department deleted successfully!");
            }
            return RedirectToAction(nameof(Departments));
        }
        #endregion

        #region Student Management
        public async Task<IActionResult> Students()
        {
            var students = await _context.Students
                .Include(s => s.Department)
                .Include(s => s.Batch)
                .Include(s => s.Section)
                .ToListAsync();
            // Views are organized under Views/Admin/Students/Index.cshtml
            // Return the explicit path so the view engine finds the Index inside the Students subfolder
            return View("~/Views/Admin/Students/Index.cshtml", students);
        }

        public async Task<IActionResult> CreateStudent()
        {
            ViewData["DepartmentId"] = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name");
            ViewData["BatchId"] = new SelectList(await _context.Batches.ToListAsync(), "Id", "Year");
            ViewData["SectionId"] = new SelectList(await _context.Sections.ToListAsync(), "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStudent(Student student)
        {
            if (ModelState.IsValid)
            {
                // Check if roll number already exists
                if (await _context.Students.AnyAsync(s => s.RollNumber == student.RollNumber))
                {
                    ModelState.AddModelError("RollNumber", "Roll number already exists.");
                    await PopulateStudentDropdowns();
                    return View(student);
                }

                student.CreatedAt = DateTime.UtcNow;
                // Use the DbSet to add the entity so the correct EF Core Add method is used
                _context.Students.Add(student);
                await _context.SaveChangesAsync();
                ShowMessage("Student created successfully!");
                return RedirectToAction(nameof(Students));
            }
            await PopulateStudentDropdowns();
            return View(student);
        }

        // This is a synchronous GET action; remove async to avoid warning
        public IActionResult UploadStudents()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadStudents(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                ShowMessage("Please select a file.", "error");
                return View();
            }

            if (!file.FileName.EndsWith(".xlsx"))
            {
                ShowMessage("Please upload an Excel file (.xlsx).", "error");
                return View();
            }

            var result = await _excelService.ImportStudentsFromExcelAsync(file.OpenReadStream(), file.FileName, GetCurrentUserId());

            if (result.Success)
            {
                ShowMessage(result.Message);
            }
            else
            {
                ShowMessage(result.Message, "error");
            }

            return View();
        }
        #endregion

        #region Reports
        public async Task<IActionResult> Reports()
        {
            ViewData["Courses"] = new SelectList(await _context.Courses.ToListAsync(), "Id", "Code");
            ViewData["Batches"] = new SelectList(await _context.Batches.Include(b => b.Department).ToListAsync(), "Id", "Year");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ExportAttendanceExcel(int? courseId, DateOnly? fromDate, DateOnly? toDate)
        {
            try
            {
                var fileBytes = await _excelService.ExportAttendanceToExcelAsync(courseId, fromDate, toDate);
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Attendance_Report_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                ShowMessage($"Error generating Excel report: {ex.Message}", "error");
                return RedirectToAction(nameof(Reports));
            }
        }

        [HttpPost]
        public async Task<IActionResult> GenerateStudentReport(int studentId, DateOnly? fromDate, DateOnly? toDate)
        {
            try
            {
                var pdfBytes = await _reportService.GenerateStudentAttendanceReportAsync(studentId, fromDate, toDate);
                return File(pdfBytes, "application/pdf", $"Student_Report_{studentId}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");
            }
            catch (Exception ex)
            {
                ShowMessage($"Error generating PDF report: {ex.Message}", "error");
                return RedirectToAction(nameof(Reports));
            }
        }
        #endregion

        #region Utility Methods
        private bool DepartmentExists(int id)
        {
            return _context.Departments.Any(e => e.Id == id);
        }

        private async Task PopulateStudentDropdowns()
        {
            ViewData["DepartmentId"] = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name");
            ViewData["BatchId"] = new SelectList(await _context.Batches.ToListAsync(), "Id", "Year");
            ViewData["SectionId"] = new SelectList(await _context.Sections.ToListAsync(), "Id", "Name");
        }

        [HttpGet]
        public async Task<JsonResult> GetBatchesByDepartment(int departmentId)
        {
            var batches = await _context.Batches
                .Where(b => b.DepartmentId == departmentId)
                .Select(b => new { b.Id, b.Year })
                .ToListAsync();
            return Json(batches);
        }

        [HttpGet]
        public async Task<JsonResult> GetSectionsByBatch(int batchId)
        {
            var sections = await _context.Sections
                .Where(s => s.BatchId == batchId)
                .Select(s => new { s.Id, s.Name })
                .ToListAsync();
            return Json(sections);
        }

        [HttpGet]
        public async Task<JsonResult> GetAllDepartments()
        {
            var departments = await _context.Departments
                .Select(d => new { d.Id, d.Name, d.Code })
                .ToListAsync();
            return Json(departments);
        }
        #endregion

        #region Batch Management
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBatch(Batch batch)
        {
            if (ModelState.IsValid)
            {
                batch.CreatedAt = DateTime.UtcNow;
                _context.Batches.Add(batch);
                await _context.SaveChangesAsync();
                ShowMessage("Batch created successfully!");
                return RedirectToAction(nameof(CreateDepartment));
            }
            ShowMessage("Error creating batch. Please check your input.", "error");
            return RedirectToAction(nameof(CreateDepartment));
        }
        #endregion

        #region Section Management
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSection(Section section)
        {
            if (ModelState.IsValid)
            {
                section.CreatedAt = DateTime.UtcNow;
                _context.Sections.Add(section);
                await _context.SaveChangesAsync();
                ShowMessage("Section created successfully!");
                return RedirectToAction(nameof(CreateDepartment));
            }
            ShowMessage("Error creating section. Please check your input.", "error");
            return RedirectToAction(nameof(CreateDepartment));
        }
        #endregion

        #region Teacher Management
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTeacherQuick(string FullName, string Email, string EmployeeId, 
            string PhoneNumber, int DepartmentId, string Password)
        {
            try
            {
                // Check if user already exists
                var userManager = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
                var roleManager = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>>();

                var existingUser = await userManager.FindByEmailAsync(Email);
                if (existingUser != null)
                {
                    ShowMessage("A user with this email already exists.", "error");
                    return RedirectToAction(nameof(CreateDepartment));
                }

                // Create ApplicationUser
                var user = new ApplicationUser
                {
                    UserName = Email,
                    Email = Email,
                    EmailConfirmed = true,
                    FullName = FullName,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(user, Password);
                if (result.Succeeded)
                {
                    // Ensure Teacher role exists
                    if (!await roleManager.RoleExistsAsync("Teacher"))
                    {
                        await roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole("Teacher"));
                    }

                    // Add user to Teacher role
                    await userManager.AddToRoleAsync(user, "Teacher");

                    // Create Teacher record
                    var teacher = new Teacher
                    {
                        UserId = user.Id,
                        FullName = FullName,
                        Email = Email,
                        EmployeeId = string.IsNullOrWhiteSpace(EmployeeId) ? $"T{new Random().Next(10000, 99999)}" : EmployeeId,
                        PhoneNumber = PhoneNumber,
                        DepartmentId = DepartmentId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Teachers.Add(teacher);
                    await _context.SaveChangesAsync();

                    ShowMessage($"Teacher created successfully! Login: {Email}, Password: {Password}");
                    return RedirectToAction(nameof(CreateDepartment));
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    ShowMessage($"Error creating teacher: {errors}", "error");
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Error creating teacher: {ex.Message}", "error");
            }

            return RedirectToAction(nameof(CreateDepartment));
        }
        #endregion

        #region Course Management
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCourse(Course course)
        {
            if (ModelState.IsValid)
            {
                // Check if course code already exists
                if (await _context.Courses.AnyAsync(c => c.Code == course.Code))
                {
                    ShowMessage("A course with this code already exists.", "error");
                    return RedirectToAction(nameof(CreateDepartment));
                }

                course.CreatedAt = DateTime.UtcNow;
                _context.Courses.Add(course);
                await _context.SaveChangesAsync();
                ShowMessage("Course created successfully!");
                return RedirectToAction(nameof(CreateDepartment));
            }
            ShowMessage("Error creating course. Please check your input.", "error");
            return RedirectToAction(nameof(CreateDepartment));
        }
        #endregion
    }
}