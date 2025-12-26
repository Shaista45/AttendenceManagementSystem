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

        public async Task<IActionResult> CreateDepartment()
        {
            // Load teachers for advanced mode
            var teachers = await _context.Teachers
                .Select(t => new
                {
                    Id = t.Id,
                    FullName = t.FullName,
                    EmployeeId = t.EmployeeId
                })
                .ToListAsync();
            
            ViewBag.Teachers = teachers;
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

            var department = await _context.Departments
                .Include(d => d.Batches!)
                    .ThenInclude(b => b.Sections)
                .Include(d => d.Batches!)
                    .ThenInclude(b => b.Students)
                .Include(d => d.Courses!)
                    .ThenInclude(c => c.Enrollments)
                .Include(d => d.Teachers)
                .Include(d => d.Students)
                .FirstOrDefaultAsync(d => d.Id == id);
                
            if (department == null)
                return NotFound();

            return View("~/Views/Admin/Departments/EditDepartment.cshtml", department);
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
                .Include(d => d.Batches)
                .Include(d => d.Courses)
                .Include(d => d.Teachers)
                .Include(d => d.Students)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (department == null)
                return NotFound();

            return View("~/Views/Admin/Departments/DeleteDepartment.cshtml", department);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteDepartment(int id)
        {
            try
            {
                var department = await _context.Departments
                    .Include(d => d.Batches)
                    .Include(d => d.Courses)
                    .Include(d => d.Teachers)
                    .Include(d => d.Students)
                    .FirstOrDefaultAsync(d => d.Id == id);
                    
                if (department == null)
                    return Json(new { success = false, message = "Department not found" });

                // Check if department has related data
                var hasRelatedData = (department.Batches?.Any() ?? false) || 
                                    (department.Courses?.Any() ?? false) || 
                                    (department.Teachers?.Any() ?? false) || 
                                    (department.Students?.Any() ?? false);

                if (hasRelatedData)
                {
                    // Delete related data (cascade delete)
                    // Note: Make sure your database is configured for cascade delete or handle manually
                    _context.Departments.Remove(department);
                }
                else
                {
                    _context.Departments.Remove(department);
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Department deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error deleting department: {ex.Message}" });
            }
        }

        [HttpPost, ActionName("DeleteDepartmentConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDepartmentConfirmed(int id)
        {
            try
            {
                var department = await _context.Departments
                    .Include(d => d.Batches!)
                        .ThenInclude(b => b.Sections!)
                            .ThenInclude(s => s.Students)
                    .Include(d => d.Batches!)
                        .ThenInclude(b => b.Students)
                    .Include(d => d.Courses!)
                        .ThenInclude(c => c.Enrollments)
                    .Include(d => d.Teachers)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (department == null)
                {
                    ShowMessage("Department not found!", "error");
                    return RedirectToAction(nameof(Departments));
                }

                // Delete all related entities in the correct order to avoid FK constraint violations
                
                // 1. Delete all attendances related to students and courses in this department
                var studentIds = new List<int>();
                var courseIds = new List<int>();

                if (department.Batches != null)
                {
                    foreach (var batch in department.Batches)
                    {
                        if (batch.Students != null)
                        {
                            studentIds.AddRange(batch.Students.Select(s => s.Id));
                        }
                    }
                }

                if (department.Courses != null)
                {
                    courseIds.AddRange(department.Courses.Select(c => c.Id));
                }

                // Delete all attendances for these students and courses
                if (studentIds.Any() || courseIds.Any())
                {
                    var attendances = await _context.Attendances
                        .Where(a => studentIds.Contains(a.StudentId) || courseIds.Contains(a.CourseId))
                        .ToListAsync();
                    _context.Attendances.RemoveRange(attendances);
                }

                // 2. Delete all enrollments for these students and courses
                if (studentIds.Any() || courseIds.Any())
                {
                    var enrollments = await _context.Enrollments
                        .Where(e => studentIds.Contains(e.StudentId) || courseIds.Contains(e.CourseId))
                        .ToListAsync();
                    _context.Enrollments.RemoveRange(enrollments);
                }

                // 3. Delete all timetables related to courses, teachers, and sections in this department
                var sectionIds = new List<int>();
                var teacherIds = new List<int>();

                if (department.Batches != null)
                {
                    foreach (var batch in department.Batches)
                    {
                        if (batch.Sections != null)
                        {
                            sectionIds.AddRange(batch.Sections.Select(s => s.Id));
                        }
                    }
                }

                if (department.Teachers != null)
                {
                    teacherIds.AddRange(department.Teachers.Select(t => t.Id));
                }

                if (sectionIds.Any() || teacherIds.Any() || courseIds.Any())
                {
                    var timetables = await _context.Timetables
                        .Where(t => sectionIds.Contains(t.SectionId) || 
                                   teacherIds.Contains(t.TeacherId) || 
                                   courseIds.Contains(t.CourseId))
                        .ToListAsync();
                    _context.Timetables.RemoveRange(timetables);
                }

                // 4. Delete students
                if (department.Batches != null)
                {
                    foreach (var batch in department.Batches)
                    {
                        if (batch.Students != null)
                        {
                            _context.Students.RemoveRange(batch.Students);
                        }
                    }
                }

                // 5. Delete sections
                if (department.Batches != null)
                {
                    foreach (var batch in department.Batches)
                    {
                        if (batch.Sections != null)
                        {
                            _context.Sections.RemoveRange(batch.Sections);
                        }
                    }
                }

                // 6. Delete batches
                if (department.Batches != null)
                {
                    _context.Batches.RemoveRange(department.Batches);
                }

                // 7. Delete courses
                if (department.Courses != null)
                {
                    _context.Courses.RemoveRange(department.Courses);
                }

                // 8. Update teachers (set department to null instead of deleting them)
                // Note: Since DepartmentId is required, we cannot just set it to null
                // Teachers will be removed as well since they cannot exist without a department
                if (department.Teachers != null && department.Teachers.Any())
                {
                    ShowMessage("Warning: This department has teachers assigned. Consider reassigning them before deletion.", "warning");
                    // Optionally: _context.Teachers.RemoveRange(department.Teachers);
                    // For now, we'll just let the FK constraint handle it
                }

                // 9. Finally delete the department
                _context.Departments.Remove(department);
                await _context.SaveChangesAsync();
                
                ShowMessage("Department and all related data deleted successfully!");
                return RedirectToAction(nameof(Departments));
            }
            catch (Exception ex)
            {
                ShowMessage($"Error deleting department: {ex.Message}", "error");
                return RedirectToAction(nameof(Departments));
            }
        }

        // Quick Add Actions
        public async Task<IActionResult> QuickAddBatch(int departmentId)
        {
            var department = await _context.Departments.FindAsync(departmentId);
            if (department == null)
                return NotFound();

            ViewBag.DepartmentId = departmentId;
            ViewBag.DepartmentName = department.Name;
            return PartialView("~/Views/Admin/Departments/_QuickAddBatch.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickAddBatch(int departmentId, string year, string description)
        {
            if (ModelState.IsValid)
            {
                var batch = new Batch
                {
                    DepartmentId = departmentId,
                    Year = year,
                    Description = description,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Batches.Add(batch);
                await _context.SaveChangesAsync();
                ShowMessage("Batch added successfully!");
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Invalid data" });
        }

        public async Task<IActionResult> QuickAddSection(int departmentId)
        {
            var department = await _context.Departments.FindAsync(departmentId);
            if (department == null)
                return NotFound();

            ViewBag.DepartmentId = departmentId;
            ViewBag.DepartmentName = department.Name;
            ViewBag.Batches = new SelectList(await _context.Batches.Where(b => b.DepartmentId == departmentId).ToListAsync(), "Id", "Year");
            return PartialView("~/Views/Admin/Departments/_QuickAddSection.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickAddSection(int departmentId, int batchId, string name)
        {
            if (ModelState.IsValid)
            {
                var section = new Section
                {
                    BatchId = batchId,
                    Name = name,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Sections.Add(section);
                await _context.SaveChangesAsync();
                ShowMessage("Section added successfully!");
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Invalid data" });
        }

        public async Task<IActionResult> QuickAddCourse(int departmentId)
        {
            var department = await _context.Departments.FindAsync(departmentId);
            if (department == null)
                return NotFound();

            ViewBag.DepartmentId = departmentId;
            ViewBag.DepartmentName = department.Name;
            return PartialView("~/Views/Admin/Departments/_QuickAddCourse.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickAddCourse(int departmentId, string title, string code, int credits, string description)
        {
            if (ModelState.IsValid)
            {
                var course = new Course
                {
                    DepartmentId = departmentId,
                    Title = title,
                    Code = code,
                    Credits = credits,
                    Description = description,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Courses.Add(course);
                await _context.SaveChangesAsync();
                ShowMessage("Course added successfully!");
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Invalid data" });
        }

        public async Task<IActionResult> QuickAddTeacher(int departmentId)
        {
            var department = await _context.Departments.FindAsync(departmentId);
            if (department == null)
                return NotFound();

            ViewBag.DepartmentId = departmentId;
            ViewBag.DepartmentName = department.Name;
            return PartialView("~/Views/Admin/Departments/_QuickAddTeacher.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickAddTeacher(int departmentId, string fullName, string email, string phoneNumber, string employeeId)
        {
            if (ModelState.IsValid)
            {
                var teacher = new Teacher
                {
                    DepartmentId = departmentId,
                    FullName = fullName,
                    Email = email,
                    PhoneNumber = phoneNumber,
                    EmployeeId = employeeId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Teachers.Add(teacher);
                await _context.SaveChangesAsync();
                ShowMessage("Teacher added successfully!");
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Invalid data" });
        }

        // Enhanced Department Management - Advanced Create
        public async Task<IActionResult> CreateAdvanced()
        {
            var teachers = await _context.Teachers
                .Select(t => new
                {
                    Id = t.Id,
                    FullName = t.FullName,
                    EmployeeId = t.EmployeeId
                })
                .ToListAsync();
            
            ViewBag.Teachers = teachers;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAdvanced(Department model, List<BatchDto> batches)
        {
            if (string.IsNullOrEmpty(model.Name) || string.IsNullOrEmpty(model.Code))
            {
                ShowMessage("Department Name and Code are required!", "error");
                return RedirectToAction(nameof(CreateAdvanced));
            }

            model.CreatedAt = DateTime.UtcNow;
            _context.Departments.Add(model);
            await _context.SaveChangesAsync();

            // Add batches and sections
            if (batches != null)
            {
                foreach (var batchDto in batches)
                {
                    var batch = new Batch
                    {
                        DepartmentId = model.Id,
                        Year = batchDto.Year,
                        Description = batchDto.Description,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Batches.Add(batch);
                    await _context.SaveChangesAsync();

                    if (batchDto.Sections != null)
                    {
                        foreach (var sectionDto in batchDto.Sections)
                        {
                            var section = new Section
                            {
                                BatchId = batch.Id,
                                Name = sectionDto.Name,
                                CreatedAt = DateTime.UtcNow
                            };
                            _context.Sections.Add(section);
                            await _context.SaveChangesAsync();

                            // Assign teachers to section (if you have a mapping table)
                            // This would require a SectionTeacher junction table
                        }
                    }
                }
            }

            ShowMessage("Department created successfully with batches and sections!");
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

                // Check if email already exists
                if (await _userManager.FindByEmailAsync(student.Email) != null)
                {
                    ModelState.AddModelError("Email", "Email already exists.");
                    await PopulateStudentDropdowns();
                    return View(student);
                }

                // Create user account for the student
                var user = new ApplicationUser
                {
                    UserName = student.Email,
                    Email = student.Email,
                    EmailConfirmed = true,
                    FullName = student.FullName
                };

                // Generate a default password (you can customize this)
                string defaultPassword = "Student@123";
                var result = await _userManager.CreateAsync(user, defaultPassword);

                if (result.Succeeded)
                {
                    // Assign Student role
                    await _userManager.AddToRoleAsync(user, "Student");

                    // Set student's UserId
                    student.UserId = user.Id;
                    student.CreatedAt = DateTime.UtcNow;
                    
                    // Use the DbSet to add the entity so the correct EF Core Add method is used
                    _context.Students.Add(student);
                    await _context.SaveChangesAsync();
                    
                    ShowMessage($"Student created successfully! Default password: {defaultPassword}");
                    return RedirectToAction(nameof(Students));
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
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
        
        // Reports Dashboard
        public async Task<IActionResult> Reports()
        {
            await LoadFilterDropdowns();
            return View("~/Views/Admin/Reports/Index.cshtml");
        }

        // Student Attendance Report
        [HttpGet]
        public async Task<IActionResult> StudentAttendanceReport(ReportFilterViewModel filters)
        {
            await LoadFilterDropdowns();
            
            var query = _context.Students
                .Include(s => s.Department)
                .Include(s => s.Batch)
                .Include(s => s.Section)
                .AsQueryable();

            if (filters.DepartmentId.HasValue)
                query = query.Where(s => s.DepartmentId == filters.DepartmentId);

            if (filters.BatchId.HasValue)
                query = query.Where(s => s.BatchId == filters.BatchId);

            if (filters.SectionId.HasValue)
                query = query.Where(s => s.SectionId == filters.SectionId);

            var students = await query.ToListAsync();
            var studentReports = new List<StudentAttendanceReportViewModel>();

            foreach (var student in students)
            {
                var attendanceQuery = _context.Attendances
                    .Where(a => a.StudentId == student.Id);

                if (filters.StartDate.HasValue)
                    attendanceQuery = attendanceQuery.Where(a => a.Date >= DateOnly.FromDateTime(filters.StartDate.Value));

                if (filters.EndDate.HasValue)
                    attendanceQuery = attendanceQuery.Where(a => a.Date <= DateOnly.FromDateTime(filters.EndDate.Value));

                if (filters.CourseId.HasValue)
                    attendanceQuery = attendanceQuery.Where(a => a.CourseId == filters.CourseId);

                var attendances = await attendanceQuery.ToListAsync();
                var totalClasses = attendances.Count;
                var classesAttended = attendances.Count(a => a.Status == AttendanceStatus.Present);

                studentReports.Add(new StudentAttendanceReportViewModel
                {
                    StudentId = student.Id,
                    StudentName = student.FullName,
                    RollNumber = student.RollNumber,
                    Department = student.Department?.Name ?? "",
                    Batch = student.Batch?.Year ?? "",
                    Section = student.Section?.Name ?? "",
                    TotalClasses = totalClasses,
                    ClassesAttended = classesAttended,
                    ClassesAbsent = totalClasses - classesAttended,
                    AttendancePercentage = totalClasses > 0 ? Math.Round((decimal)classesAttended / totalClasses * 100, 2) : 0
                });
            }

            var viewModel = new StudentAttendanceReportPageViewModel
            {
                Filters = filters,
                Students = studentReports.OrderBy(s => s.StudentName).ToList()
            };

            return View("~/Views/Admin/Reports/StudentAttendance.cshtml", viewModel);
        }

        // Teacher Report
        [HttpGet]
        public async Task<IActionResult> TeacherReport(ReportFilterViewModel filters)
        {
            await LoadFilterDropdowns();
            
            var query = _context.Teachers
                .Include(t => t.Department)
                .Where(t => t.IsActive);

            if (filters.DepartmentId.HasValue)
                query = query.Where(t => t.DepartmentId == filters.DepartmentId);

            var teachers = await query.ToListAsync();
            var teacherReports = new List<TeacherReportViewModel>();

            foreach (var teacher in teachers)
            {
                var timetableQuery = _context.Timetables
                    .Where(tt => tt.TeacherId == teacher.Id);

                if (filters.CourseId.HasValue)
                    timetableQuery = timetableQuery.Where(tt => tt.CourseId == filters.CourseId);

                var timetables = await timetableQuery.ToListAsync();
                var assignedCourses = timetables.Select(tt => tt.CourseId).Distinct().Count();

                var courseIds = timetables.Select(tt => tt.CourseId).Distinct().ToList();
                var attendanceQuery = _context.Attendances
                    .Where(a => courseIds.Contains(a.CourseId));

                if (filters.StartDate.HasValue)
                    attendanceQuery = attendanceQuery.Where(a => a.Date >= DateOnly.FromDateTime(filters.StartDate.Value));

                if (filters.EndDate.HasValue)
                    attendanceQuery = attendanceQuery.Where(a => a.Date <= DateOnly.FromDateTime(filters.EndDate.Value));

                var totalClasses = await attendanceQuery.Select(a => new { a.CourseId, a.Date }).Distinct().CountAsync();
                var markedClasses = await attendanceQuery.CountAsync();

                teacherReports.Add(new TeacherReportViewModel
                {
                    TeacherId = teacher.Id,
                    TeacherName = teacher.FullName,
                    EmployeeId = teacher.EmployeeId ?? "",
                    Department = teacher.Department?.Name ?? "",
                    AssignedCourses = assignedCourses,
                    TotalClassesTaken = totalClasses,
                    AttendanceMarked = markedClasses,
                    AttendancePending = 0,
                    MarkingPercentage = markedClasses > 0 ? 100 : 0
                });
            }

            var viewModel = new TeacherReportPageViewModel
            {
                Filters = filters,
                Teachers = teacherReports.OrderBy(t => t.TeacherName).ToList()
            };

            return View("~/Views/Admin/Reports/TeacherReport.cshtml", viewModel);
        }

        // Course Report
        [HttpGet]
        public async Task<IActionResult> CourseReport(ReportFilterViewModel filters)
        {
            await LoadFilterDropdowns();
            
            var query = _context.Courses
                .Include(c => c.Department)
                .AsQueryable();

            if (filters.DepartmentId.HasValue)
                query = query.Where(c => c.DepartmentId == filters.DepartmentId);

            if (filters.CourseId.HasValue)
                query = query.Where(c => c.Id == filters.CourseId);

            var courses = await query.ToListAsync();
            var courseReports = new List<CourseReportViewModel>();

            foreach (var course in courses)
            {
                var attendanceQuery = _context.Attendances
                    .Where(a => a.CourseId == course.Id);

                if (filters.StartDate.HasValue)
                    attendanceQuery = attendanceQuery.Where(a => a.Date >= DateOnly.FromDateTime(filters.StartDate.Value));

                if (filters.EndDate.HasValue)
                    attendanceQuery = attendanceQuery.Where(a => a.Date <= DateOnly.FromDateTime(filters.EndDate.Value));

                var attendances = await attendanceQuery.ToListAsync();
                var totalLectures = attendances.Select(a => a.Date).Distinct().Count();
                var totalStudents = attendances.Select(a => a.StudentId).Distinct().Count();
                var averageAttendance = attendances.Any() 
                    ? Math.Round((decimal)attendances.Count(a => a.Status == AttendanceStatus.Present) / attendances.Count * 100, 2)
                    : 0;

                // Calculate defaulters (students with < 75% attendance)
                var studentAttendance = attendances
                    .GroupBy(a => a.StudentId)
                    .Select(g => new
                    {
                        StudentId = g.Key,
                        Total = g.Count(),
                        Present = g.Count(a => a.Status == AttendanceStatus.Present),
                        Percentage = g.Count() > 0 ? (decimal)g.Count(a => a.Status == AttendanceStatus.Present) / g.Count() * 100 : 0
                    });

                var defaulters = studentAttendance.Count(sa => sa.Percentage < 75);

                courseReports.Add(new CourseReportViewModel
                {
                    CourseId = course.Id,
                    CourseCode = course.Code,
                    CourseTitle = course.Title,
                    Department = course.Department?.Name ?? "",
                    TotalLectures = totalLectures,
                    TotalStudents = totalStudents,
                    AverageAttendance = averageAttendance,
                    NumberOfDefaulters = defaulters
                });
            }

            var viewModel = new CourseReportPageViewModel
            {
                Filters = filters,
                Courses = courseReports.OrderBy(c => c.CourseCode).ToList()
            };

            return View("~/Views/Admin/Reports/CourseReport.cshtml", viewModel);
        }

        // Daily Attendance Report
        [HttpGet]
        public async Task<IActionResult> DailyAttendanceReport(ReportFilterViewModel filters)
        {
            await LoadFilterDropdowns();
            
            var attendanceQuery = _context.Attendances
                .Include(a => a.Course)
                .Include(a => a.Student)
                    .ThenInclude(s => s.Section)
                .AsQueryable();

            if (filters.StartDate.HasValue)
                attendanceQuery = attendanceQuery.Where(a => a.Date >= DateOnly.FromDateTime(filters.StartDate.Value));

            if (filters.EndDate.HasValue)
                attendanceQuery = attendanceQuery.Where(a => a.Date <= DateOnly.FromDateTime(filters.EndDate.Value));

            if (filters.CourseId.HasValue)
                attendanceQuery = attendanceQuery.Where(a => a.CourseId == filters.CourseId);

            var attendances = await attendanceQuery.ToListAsync();

            var dailyRecords = attendances
                .GroupBy(a => new { a.Date, a.CourseId, SectionId = a.Student!.SectionId })
                .Select(g => new DailyAttendanceReportViewModel
                {
                    Date = g.Key.Date.ToDateTime(TimeOnly.MinValue),
                    CourseName = g.First().Course?.Title ?? "",
                    Section = g.First().Student?.Section?.Name ?? "",
                    TeacherName = "",  // Will need to get from timetable if needed
                    TotalStudents = g.Count(),
                    PresentCount = g.Count(a => a.Status == AttendanceStatus.Present),
                    AbsentCount = g.Count(a => a.Status == AttendanceStatus.Absent),
                    AttendancePercentage = g.Count() > 0 ? Math.Round((decimal)g.Count(a => a.Status == AttendanceStatus.Present) / g.Count() * 100, 2) : 0
                })
                .OrderByDescending(d => d.Date)
                .ToList();

            var viewModel = new DailyAttendanceReportPageViewModel
            {
                Filters = filters,
                DailyRecords = dailyRecords
            };

            return View("~/Views/Admin/Reports/DailyAttendance.cshtml", viewModel);
        }

        // Low Attendance (Defaulter) Report
        [HttpGet]
        public async Task<IActionResult> LowAttendanceReport(ReportFilterViewModel filters)
        {
            await LoadFilterDropdowns();
            
            var query = _context.Students
                .Include(s => s.Department)
                .Include(s => s.Batch)
                .Include(s => s.Section)
                .AsQueryable();

            if (filters.DepartmentId.HasValue)
                query = query.Where(s => s.DepartmentId == filters.DepartmentId);

            if (filters.BatchId.HasValue)
                query = query.Where(s => s.BatchId == filters.BatchId);

            if (filters.SectionId.HasValue)
                query = query.Where(s => s.SectionId == filters.SectionId);

            var students = await query.ToListAsync();
            var defaulters = new List<LowAttendanceReportViewModel>();

            foreach (var student in students)
            {
                var attendanceQuery = _context.Attendances
                    .Include(a => a.Course)
                    .Where(a => a.StudentId == student.Id);

                if (filters.StartDate.HasValue)
                    attendanceQuery = attendanceQuery.Where(a => a.Date >= DateOnly.FromDateTime(filters.StartDate.Value));

                if (filters.EndDate.HasValue)
                    attendanceQuery = attendanceQuery.Where(a => a.Date <= DateOnly.FromDateTime(filters.EndDate.Value));

                if (filters.CourseId.HasValue)
                    attendanceQuery = attendanceQuery.Where(a => a.CourseId == filters.CourseId);

                var attendances = await attendanceQuery.ToListAsync();

                // Group by course
                var courseGroups = attendances.GroupBy(a => a.CourseId);

                foreach (var courseGroup in courseGroups)
                {
                    var totalClasses = courseGroup.Count();
                    var classesAttended = courseGroup.Count(a => a.Status == AttendanceStatus.Present);
                    var percentage = totalClasses > 0 ? (decimal)classesAttended / totalClasses * 100 : 0;

                    if (percentage < 75 && totalClasses > 0)
                    {
                        var courseName = courseGroup.First().Course?.Title ?? "";
                        var requiredClasses = (int)Math.Ceiling(totalClasses * 0.75m);
                        var shortfall = requiredClasses - classesAttended;

                        defaulters.Add(new LowAttendanceReportViewModel
                        {
                            StudentId = student.Id,
                            StudentName = student.FullName,
                            RollNumber = student.RollNumber,
                            Department = student.Department?.Name ?? "",
                            Batch = student.Batch?.Year ?? "",
                            Section = student.Section?.Name ?? "",
                            CourseName = courseName,
                            TotalClasses = totalClasses,
                            ClassesAttended = classesAttended,
                            AttendancePercentage = Math.Round(percentage, 2),
                            ShortfallClasses = shortfall
                        });
                    }
                }
            }

            var viewModel = new LowAttendanceReportPageViewModel
            {
                Filters = filters,
                Defaulters = defaulters.OrderBy(d => d.AttendancePercentage).ToList()
            };

            return View("~/Views/Admin/Reports/LowAttendance.cshtml", viewModel);
        }

        // Summary Analytics
        [HttpGet]
        public async Task<IActionResult> SummaryAnalytics()
        {
            var viewModel = new SummaryAnalyticsViewModel
            {
                TotalStudents = await _context.Students.CountAsync(),
                TotalTeachers = await _context.Teachers.CountAsync(t => t.IsActive),
                TotalCourses = await _context.Courses.CountAsync(),
                TotalDepartments = await _context.Departments.CountAsync(),
                TotalClassesConducted = await _context.Attendances
                    .Select(a => new { a.CourseId, a.Date })
                    .Distinct()
                    .CountAsync(),
                TotalAttendanceMarked = await _context.Attendances.CountAsync()
            };

            var allAttendances = await _context.Attendances.ToListAsync();
            viewModel.OverallAttendancePercentage = allAttendances.Any()
                ? Math.Round((decimal)allAttendances.Count(a => a.Status == AttendanceStatus.Present) / allAttendances.Count * 100, 2)
                : 0;

            // Student performance categorization
            var students = await _context.Students.ToListAsync();
            foreach (var student in students)
            {
                var studentAttendances = allAttendances.Where(a => a.StudentId == student.Id).ToList();
                if (studentAttendances.Any())
                {
                    var percentage = (decimal)studentAttendances.Count(a => a.Status == AttendanceStatus.Present) / studentAttendances.Count * 100;
                    
                    if (percentage >= 75)
                        viewModel.StudentsAbove75Percent++;
                    else if (percentage >= 50)
                        viewModel.StudentsBelow75Percent++;
                    else
                        viewModel.StudentsBelow50Percent++;
                }
            }

            // Department-wise statistics
            var departments = await _context.Departments.ToListAsync();
            foreach (var dept in departments)
            {
                var deptStudents = students.Where(s => s.DepartmentId == dept.Id).ToList();
                var deptAttendances = allAttendances.Where(a => deptStudents.Select(s => s.Id).Contains(a.StudentId)).ToList();
                
                viewModel.DepartmentStats.Add(new DepartmentStatistic
                {
                    DepartmentName = dept.Name,
                    StudentCount = deptStudents.Count,
                    AverageAttendance = deptAttendances.Any()
                        ? Math.Round((decimal)deptAttendances.Count(a => a.Status == AttendanceStatus.Present) / deptAttendances.Count * 100, 2)
                        : 0
                });
            }

            viewModel.LastAttendanceDate = allAttendances.Any()
                ? allAttendances.Max(a => a.Date).ToDateTime(TimeOnly.MinValue)
                : null;

            viewModel.TodayClasses = allAttendances
                .Where(a => a.Date == DateOnly.FromDateTime(DateTime.Today))
                .Select(a => new { a.CourseId, a.Date })
                .Distinct()
                .Count();

            return View("~/Views/Admin/Reports/SummaryAnalytics.cshtml", viewModel);
        }

        // Export to Excel
        [HttpPost]
        public async Task<IActionResult> ExportToExcel(string reportType, ReportFilterViewModel filters)
        {
            try
            {
                byte[] fileBytes;
                string fileName;

                switch (reportType)
                {
                    case "student":
                        fileBytes = await _excelService.ExportAttendanceToExcelAsync(filters.CourseId, 
                            filters.StartDate.HasValue ? DateOnly.FromDateTime(filters.StartDate.Value) : null,
                            filters.EndDate.HasValue ? DateOnly.FromDateTime(filters.EndDate.Value) : null);
                        fileName = $"Student_Attendance_Report_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
                        break;

                    default:
                        ShowErrorMessage("Invalid report type.");
                        return RedirectToAction(nameof(Reports));
                }

                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error generating Excel report: {ex.Message}");
                return RedirectToAction(nameof(Reports));
            }
        }

        // Export to PDF
        [HttpPost]
        public async Task<IActionResult> ExportToPdf(string reportType, int? entityId, ReportFilterViewModel filters)
        {
            try
            {
                byte[] pdfBytes;
                string fileName;

                switch (reportType)
                {
                    case "student":
                        if (!entityId.HasValue)
                        {
                            ShowErrorMessage("Student ID is required.");
                            return RedirectToAction(nameof(Reports));
                        }
                        pdfBytes = await _reportService.GenerateStudentAttendanceReportAsync(
                            entityId.Value,
                            filters.StartDate.HasValue ? DateOnly.FromDateTime(filters.StartDate.Value) : null,
                            filters.EndDate.HasValue ? DateOnly.FromDateTime(filters.EndDate.Value) : null);
                        fileName = $"Student_Report_{entityId}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
                        break;

                    default:
                        ShowErrorMessage("Invalid report type.");
                        return RedirectToAction(nameof(Reports));
                }

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error generating PDF report: {ex.Message}");
                return RedirectToAction(nameof(Reports));
            }
        }

        // Helper method to load filter dropdowns
        private async Task LoadFilterDropdowns()
        {
            ViewData["Departments"] = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name");
            ViewData["Batches"] = new SelectList(await _context.Batches.ToListAsync(), "Id", "Year");
            ViewData["Sections"] = new SelectList(await _context.Sections.ToListAsync(), "Id", "Name");
            ViewData["Courses"] = new SelectList(await _context.Courses.ToListAsync(), "Id", "Title");
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
        public IActionResult CreateBatch(int departmentId)
        {
            var batch = new Batch { DepartmentId = departmentId };
            return PartialView("~/Views/Admin/Batches/_CreateBatchPartial.cshtml", batch);
        }

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
                return Json(new { success = true });
            }
            var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
            return Json(new { success = false, errors = errors });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBatch(int id)
        {
            try
            {
                var batch = await _context.Batches
                    .Include(b => b.Sections!)
                        .ThenInclude(s => s.Students)
                    .Include(b => b.Students)
                    .FirstOrDefaultAsync(b => b.Id == id);

                if (batch == null)
                    return Json(new { success = false, message = "Batch not found" });

                _context.Batches.Remove(batch);
                await _context.SaveChangesAsync();
                ShowMessage("Batch deleted successfully!");
                return Json(new { success = true, message = "Batch deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error deleting batch: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditBatch(int? id)
        {
            if (id == null)
                return NotFound();

            var batch = await _context.Batches
                .Include(b => b.Department)
                .Include(b => b.Sections)
                .Include(b => b.Students)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (batch == null)
                return NotFound();

            return View("~/Views/Admin/Batches/EditBatch.cshtml", batch);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBatch(int id, Batch batch)
        {
            if (id != batch.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(batch);
                    await _context.SaveChangesAsync();
                    ShowMessage("Batch updated successfully!");
                    return RedirectToAction("EditDepartment", new { id = batch.DepartmentId });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Batches.Any(b => b.Id == batch.Id))
                        return NotFound();
                    throw;
                }
            }
            return View("~/Views/Admin/Batches/EditBatch.cshtml", batch);
        }

        [HttpGet]
        public async Task<IActionResult> ViewBatch(int? id)
        {
            if (id == null)
                return NotFound();

            var batch = await _context.Batches
                .Include(b => b.Department)
                .Include(b => b.Sections!)
                    .ThenInclude(s => s.Students)
                .Include(b => b.Students)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (batch == null)
                return NotFound();

            return View("~/Views/Admin/Batches/ViewBatch.cshtml", batch);
        }
        #endregion

        #region Course Management
        public async Task<IActionResult> Courses()
        {
            var courses = await _context.Courses
                .Include(c => c.Department)
                .ToListAsync();
            return View("~/Views/Admin/Courses/Index.cshtml", courses);
        }

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCourseQuick(Course course)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Check if course code already exists
                    if (await _context.Courses.AnyAsync(c => c.Code == course.Code))
                    {
                        return Json(new { success = false, message = "A course with this code already exists." });
                    }

                    // Validate department exists
                    if (!await _context.Departments.AnyAsync(d => d.Id == course.DepartmentId))
                    {
                        return Json(new { success = false, message = "Invalid department." });
                    }

                    course.CreatedAt = DateTime.UtcNow;
                    _context.Courses.Add(course);
                    await _context.SaveChangesAsync();
                    ShowMessage("Course created successfully!");
                    return Json(new { success = true, message = "Course created successfully!" });
                }

                var errors = string.Join(", ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                return Json(new { success = false, message = errors });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error creating course: {ex.Message}" });
            }
        }

        public async Task<IActionResult> EditCourse(int? id)
        {
            if (id == null)
                return NotFound();

            var course = await _context.Courses
                .Include(c => c.Department)
                .Include(c => c.Timetables)
                .Include(c => c.Enrollments)
                .FirstOrDefaultAsync(c => c.Id == id);
                
            if (course == null)
                return NotFound();

            ViewData["DepartmentId"] = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name", course.DepartmentId);
            return View("~/Views/Admin/Courses/EditCourse.cshtml", course);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCourse(int id, Course course)
        {
            if (id != course.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Check if course code is being changed and already exists
                    if (await _context.Courses.AnyAsync(c => c.Code == course.Code && c.Id != course.Id))
                    {
                        ShowMessage("A course with this code already exists.", "error");
                        ViewData["DepartmentId"] = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name", course.DepartmentId);
                        return View("~/Views/Admin/Courses/EditCourse.cshtml", course);
                    }

                    _context.Update(course);
                    await _context.SaveChangesAsync();
                    ShowMessage("Course updated successfully!");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Courses.Any(e => e.Id == course.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Departments));
            }
            ViewData["DepartmentId"] = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name", course.DepartmentId);
            return View("~/Views/Admin/Courses/EditCourse.cshtml", course);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            try
            {
                var course = await _context.Courses
                    .Include(c => c.Timetables)
                    .Include(c => c.Enrollments)
                    .Include(c => c.Attendances)
                    .FirstOrDefaultAsync(c => c.Id == id);
                    
                if (course == null)
                    return Json(new { success = false, message = "Course not found" });

                _context.Courses.Remove(course);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Course deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error deleting course: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ViewCourse(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var course = await _context.Courses
                .Include(c => c.Department)
                .Include(c => c.Enrollments!)
                .ThenInclude(e => e.Student)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (course == null)
            {
                return NotFound();
            }

            return View("~/Views/Admin/Courses/ViewCourse.cshtml", course);
        }
        #endregion

        #region Teacher Management
        [HttpGet]
        public async Task<IActionResult> ViewTeacher(int? id)
        {
            if (id == null)
                return NotFound();

            var teacher = await _context.Teachers
                .Include(t => t.Department)
                .Include(t => t.User)
                .Include(t => t.Timetables!)
                    .ThenInclude(tt => tt.Course)
                .Include(t => t.Timetables!)
                    .ThenInclude(tt => tt.Section)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (teacher == null)
                return NotFound();

            return View("~/Views/Admin/Teachers/ViewTeacher.cshtml", teacher);
        }

        [HttpGet]
        public IActionResult AddTeacher(int? departmentId)
        {
            ViewBag.Departments = new SelectList(_context.Departments.ToList(), "Id", "Name", departmentId);
            var teacher = new Teacher();
            if (departmentId.HasValue)
                teacher.DepartmentId = departmentId.Value;
            
            return View("~/Views/Admin/Teachers/AddTeacher.cshtml", teacher);
        }

        [HttpGet]
        public async Task<IActionResult> EditTeacher(int? id)
        {
            if (id == null)
                return NotFound();

            var teacher = await _context.Teachers
                .Include(t => t.Department)
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (teacher == null)
                return NotFound();

            ViewBag.Departments = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name", teacher.DepartmentId);
            return View("~/Views/Admin/Teachers/EditTeacher.cshtml", teacher);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTeacher(int id, Teacher teacher)
        {
            if (id != teacher.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(teacher);
                    await _context.SaveChangesAsync();
                    ShowMessage("Teacher updated successfully!");
                    return RedirectToAction("EditDepartment", new { id = teacher.DepartmentId });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Teachers.Any(t => t.Id == teacher.Id))
                        return NotFound();
                    throw;
                }
            }
            ViewBag.Departments = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name", teacher.DepartmentId);
            return View("~/Views/Admin/Teachers/EditTeacher.cshtml", teacher);
        }
        #endregion

        #region Teacher Management
        public async Task<IActionResult> Teachers()
        {
            var teachers = await _context.Teachers
                .Include(t => t.Department)
                .Include(t => t.User)
                .Include(t => t.Timetables!)
                    .ThenInclude(tt => tt.Course)
                .Where(t => t.IsActive)
                .OrderBy(t => t.FullName)
                .ToListAsync();

            var teacherViewModels = teachers.Select(t => new TeacherViewModel
            {
                Id = t.Id,
                FullName = t.FullName,
                Email = t.Email ?? "",
                EmployeeId = t.EmployeeId,
                PhoneNumber = t.PhoneNumber,
                DepartmentId = t.DepartmentId,
                DepartmentName = t.Department?.Name ?? "N/A",
                IsApproved = t.IsApproved,
                IsActive = t.IsActive,
                Status = t.IsApproved ? "Approved" : "Pending",
                AssignedCoursesCount = t.Timetables?.GroupBy(tt => tt.CourseId).Count() ?? 0,
                CreatedAt = t.CreatedAt
            }).ToList();

            return View("~/Views/Admin/Teachers/Index.cshtml", teacherViewModels);
        }

        public async Task<IActionResult> CreateTeacher()
        {
            ViewBag.Departments = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name");
            return View("~/Views/Admin/Teachers/Create.cshtml", new TeacherViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTeacher(TeacherViewModel model, string action)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Check if email already exists
                    var existingUser = await _userManager.FindByEmailAsync(model.Email);
                    if (existingUser != null)
                    {
                        ShowErrorMessage("A user with this email already exists.");
                        ViewBag.Departments = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name", model.DepartmentId);
                        return View("~/Views/Admin/Teachers/Create.cshtml", model);
                    }

                    // Create ApplicationUser
                    var user = new ApplicationUser
                    {
                        UserName = model.Email,
                        Email = model.Email,
                        EmailConfirmed = true
                    };

                    var result = await _userManager.CreateAsync(user, model.Password!);

                    if (result.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(user, "Teacher");

                        // Create Teacher
                        var teacher = new Teacher
                        {
                            UserId = user.Id,
                            FullName = model.FullName,
                            Email = model.Email,
                            EmployeeId = model.EmployeeId,
                            PhoneNumber = model.PhoneNumber,
                            DepartmentId = model.DepartmentId,
                            IsApproved = false,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Teachers.Add(teacher);
                        await _context.SaveChangesAsync();

                        ShowMessage("Teacher created successfully! Status: Pending Approval");
                        
                        // Check which button was clicked
                        if (action == "saveAndAssign")
                        {
                            return RedirectToAction(nameof(AssignCourses), new { id = teacher.Id });
                        }
                        
                        return RedirectToAction(nameof(Teachers));
                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ShowErrorMessage($"Error creating teacher: {ex.Message}");
                }
            }

            ViewBag.Departments = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name", model.DepartmentId);
            return View("~/Views/Admin/Teachers/Create.cshtml", model);
        }

        public async Task<IActionResult> EditTeacherInfo(int? id)
        {
            if (id == null)
                return NotFound();

            var teacher = await _context.Teachers
                .Include(t => t.Department)
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (teacher == null)
                return NotFound();

            var model = new TeacherViewModel
            {
                Id = teacher.Id,
                FullName = teacher.FullName,
                Email = teacher.Email ?? "",
                EmployeeId = teacher.EmployeeId,
                PhoneNumber = teacher.PhoneNumber,
                DepartmentId = teacher.DepartmentId,
                IsApproved = teacher.IsApproved,
                IsActive = teacher.IsActive
            };

            ViewBag.Departments = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name", teacher.DepartmentId);
            return View("~/Views/Admin/Teachers/Edit.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTeacherInfo(int id, TeacherViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var teacher = await _context.Teachers
                        .Include(t => t.User)
                        .FirstOrDefaultAsync(t => t.Id == id);

                    if (teacher == null)
                        return NotFound();

                    teacher.FullName = model.FullName;
                    teacher.EmployeeId = model.EmployeeId;
                    teacher.PhoneNumber = model.PhoneNumber;
                    teacher.DepartmentId = model.DepartmentId;

                    // Update email if changed
                    if (teacher.Email != model.Email && teacher.User != null)
                    {
                        teacher.User.Email = model.Email;
                        teacher.User.UserName = model.Email;
                        teacher.Email = model.Email;
                        await _userManager.UpdateAsync(teacher.User);
                    }

                    _context.Update(teacher);
                    await _context.SaveChangesAsync();

                    ShowMessage("Teacher information updated successfully!");
                    return RedirectToAction(nameof(Teachers));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Teachers.Any(t => t.Id == id))
                        return NotFound();
                    throw;
                }
            }

            ViewBag.Departments = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name", model.DepartmentId);
            return View("~/Views/Admin/Teachers/Edit.cshtml", model);
        }

        public async Task<IActionResult> TeacherDetails(int? id)
        {
            if (id == null)
                return NotFound();

            var teacher = await _context.Teachers
                .Include(t => t.Department)
                .Include(t => t.User)
                .Include(t => t.Timetables!)
                    .ThenInclude(tt => tt.Course)
                .Include(t => t.Timetables!)
                    .ThenInclude(tt => tt.Batch)
                .Include(t => t.Timetables!)
                    .ThenInclude(tt => tt.Section)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (teacher == null)
                return NotFound();

            var model = new TeacherViewModel
            {
                Id = teacher.Id,
                FullName = teacher.FullName,
                Email = teacher.Email ?? "",
                EmployeeId = teacher.EmployeeId,
                PhoneNumber = teacher.PhoneNumber,
                DepartmentId = teacher.DepartmentId,
                DepartmentName = teacher.Department?.Name ?? "N/A",
                IsApproved = teacher.IsApproved,
                IsActive = teacher.IsActive,
                Status = teacher.IsApproved ? "Approved" : "Pending",
                AssignedCoursesCount = teacher.Timetables?.GroupBy(tt => tt.CourseId).Count() ?? 0,
                CreatedAt = teacher.CreatedAt
            };

            ViewBag.Timetables = teacher.Timetables?.Select(tt => new
            {
                tt.Id,
                CourseName = tt.Course?.Title,
                BatchName = tt.Batch?.Year.ToString(),
                SectionName = tt.Section?.Name,
                DayOfWeek = tt.DayOfWeek.ToString(),
                TimeSlot = $"{tt.StartTime:hh\\:mm} - {tt.EndTime:hh\\:mm}"
            }).ToList();

            return View("~/Views/Admin/Teachers/Details.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveTeacher(int id)
        {
            var teacher = await _context.Teachers.FindAsync(id);
            if (teacher == null)
                return NotFound();

            teacher.IsApproved = true;
            await _context.SaveChangesAsync();

            ShowMessage($"Teacher {teacher.FullName} has been approved successfully!");
            return RedirectToAction(nameof(Teachers));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableTeacher(int id)
        {
            var teacher = await _context.Teachers.FindAsync(id);
            if (teacher == null)
                return NotFound();

            teacher.IsActive = !teacher.IsActive;
            await _context.SaveChangesAsync();

            var status = teacher.IsActive ? "enabled" : "disabled";
            ShowMessage($"Teacher {teacher.FullName} has been {status} successfully!");
            return RedirectToAction(nameof(Teachers));
        }

        public async Task<IActionResult> AssignCourses(int? id)
        {
            if (id == null)
                return NotFound();

            var teacher = await _context.Teachers
                .Include(t => t.Department)
                .Include(t => t.Timetables!)
                    .ThenInclude(tt => tt.Course)
                .Include(t => t.Timetables!)
                    .ThenInclude(tt => tt.Batch)
                .Include(t => t.Timetables!)
                    .ThenInclude(tt => tt.Section)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (teacher == null)
                return NotFound();

            var model = new AssignCoursesViewModel
            {
                TeacherId = teacher.Id,
                TeacherName = teacher.FullName,
                TeacherEmail = teacher.Email ?? "",
                DepartmentId = teacher.DepartmentId,
                DepartmentName = teacher.Department?.Name ?? "",
                ExistingAssignments = teacher.Timetables?.Select(tt => new ExistingAssignmentDto
                {
                    TimetableId = tt.Id,
                    CourseName = tt.Course?.Title ?? "",
                    BatchName = tt.Batch?.Year.ToString() ?? "",
                    SectionName = tt.Section?.Name ?? "",
                    DayOfWeek = tt.DayOfWeek.ToString(),
                    TimeSlot = $"{tt.StartTime:hh\\:mm} - {tt.EndTime:hh\\:mm}"
                }).ToList() ?? new List<ExistingAssignmentDto>()
            };

            ViewBag.Courses = new SelectList(
                await _context.Courses.Where(c => c.DepartmentId == teacher.DepartmentId).ToListAsync(),
                "Id", "Title");
            
            ViewBag.Batches = new SelectList(
                await _context.Batches.Where(b => b.DepartmentId == teacher.DepartmentId).ToListAsync(),
                "Id", "Year");
            
            ViewBag.Sections = new SelectList(await _context.Sections.ToListAsync(), "Id", "Name");

            return View("~/Views/Admin/Teachers/AssignCourses.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignCourses(int teacherId, List<CourseAssignmentDto> assignments)
        {
            try
            {
                if (assignments == null || !assignments.Any())
                {
                    ShowErrorMessage("Please add at least one course assignment.");
                    return RedirectToAction(nameof(AssignCourses), new { id = teacherId });
                }

                var teacher = await _context.Teachers.FindAsync(teacherId);
                if (teacher == null)
                    return NotFound();

                foreach (var assignment in assignments)
                {
                    // Check for duplicate assignment
                    var exists = await _context.Timetables.AnyAsync(tt =>
                        tt.TeacherId == teacherId &&
                        tt.CourseId == assignment.CourseId &&
                        tt.BatchId == assignment.BatchId &&
                        tt.SectionId == assignment.SectionId);

                    if (exists)
                        continue;

                    var timetable = new Timetable
                    {
                        CourseId = assignment.CourseId,
                        TeacherId = teacherId,
                        BatchId = assignment.BatchId,
                        SectionId = assignment.SectionId,
                        DayOfWeek = assignment.DayOfWeek,
                        StartTime = assignment.StartTime,
                        EndTime = assignment.EndTime,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Timetables.Add(timetable);
                }

                await _context.SaveChangesAsync();
                ShowMessage("Courses assigned successfully!");
                return RedirectToAction(nameof(TeacherDetails), new { id = teacherId });
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error assigning courses: {ex.Message}");
                return RedirectToAction(nameof(AssignCourses), new { id = teacherId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAssignment(int timetableId, int teacherId)
        {
            try
            {
                var timetable = await _context.Timetables.FindAsync(timetableId);
                if (timetable != null)
                {
                    _context.Timetables.Remove(timetable);
                    await _context.SaveChangesAsync();
                    ShowMessage("Course assignment removed successfully!");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error removing assignment: {ex.Message}");
            }

            return RedirectToAction(nameof(AssignCourses), new { id = teacherId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTeacher(int id)
        {
            try
            {
                var teacher = await _context.Teachers
                    .Include(t => t.User)
                    .Include(t => t.Timetables)
                    .Include(t => t.Attendances)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (teacher == null)
                    return NotFound();

                // Delete related records
                if (teacher.Attendances != null && teacher.Attendances.Any())
                {
                    _context.Attendances.RemoveRange(teacher.Attendances);
                }

                if (teacher.Timetables != null && teacher.Timetables.Any())
                {
                    _context.Timetables.RemoveRange(teacher.Timetables);
                }

                // Delete teacher record
                _context.Teachers.Remove(teacher);

                // Delete associated user account
                if (teacher.User != null)
                {
                    await _userManager.DeleteAsync(teacher.User);
                }

                await _context.SaveChangesAsync();

                ShowMessage($"Teacher {teacher.FullName} has been deleted successfully!");
                return RedirectToAction(nameof(Teachers));
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error deleting teacher: {ex.Message}");
                return RedirectToAction(nameof(Teachers));
            }
        }
        #endregion
    }
}