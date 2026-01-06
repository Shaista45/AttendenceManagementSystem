// ...existing code...
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
using Microsoft.Extensions.Logging;

namespace AttendenceManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IExcelService _excelService;
        private readonly IReportService _reportService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext context, IExcelService excelService, IReportService reportService, UserManager<ApplicationUser> userManager, ILogger<AdminController> logger)
        {
            _context = context;
            _excelService = excelService;
            _reportService = reportService;
            _userManager = userManager;
            _logger = logger;
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
                .Include(d => d.Students)
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
                try
                {
                    // Check for duplicate code
                    if (await _context.Departments.AnyAsync(d => d.Code == department.Code))
                    {
                        return Json(new { 
                            success = false, 
                            message = "Department code already exists." 
                        });
                    }

                    department.CreatedAt = DateTime.UtcNow;
                    _context.Departments.Add(department);
                    await _context.SaveChangesAsync();
                    
                    // Return JSON for AJAX
                    return Json(new { 
                        success = true, 
                        message = "Department created successfully!",
                        departmentId = department.Id
                    });
                }
                catch (Exception ex)
                {
                    return Json(new { 
                        success = false, 
                        message = $"Error: {ex.Message}" 
                    });
                }
            }
            
            // Return validation errors as JSON
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return Json(new { 
                success = false, 
                message = "Validation failed", 
                errors = errors 
            });
        }

        public async Task<IActionResult>EditDepartment(int? id)
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
                return Json(new { success = false, message = "Invalid Department ID." });

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(department);
                    await _context.SaveChangesAsync();
                    
                    // Return JSON for AJAX
                    return Json(new { 
                        success = true, 
                        message = "Department updated successfully!" 
                    });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DepartmentExists(department.Id))
                        return Json(new { success = false, message = "Department not found." });
                    throw;
                }
            }
            
            // Return validation errors as JSON
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return Json(new { 
                success = false, 
                message = "Validation failed", 
                errors = errors 
            });
        }

        public async Task<IActionResult> ViewDepartment(int? id)
        {
            if (id == null)
                return NotFound();

            var department = await _context.Departments
                .Include(d => d.Batches)
                .Include(d => d.Courses)
                .Include(d => d.Teachers)
                .Include(d => d.Students!)
                    .ThenInclude(s => s.Section)
                .Include(d => d.Students!)
                    .ThenInclude(s => s.Batch)
                .FirstOrDefaultAsync(m => m.Id == id);
            
            if (department == null) return NotFound();

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSection(int batchId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Json(new { success = false, message = "Section name is required!" });
            }

            var batch = await _context.Batches.FindAsync(batchId);
            if (batch == null)
            {
                return Json(new { success = false, message = "Batch not found!" });
            }

            // Check for duplicate
            if (await _context.Sections.AnyAsync(s => s.BatchId == batchId && s.Name == name))
            {
                return Json(new { success = false, message = "Section name already exists in this batch." });
            }

            var section = new Section
            {
                BatchId = batchId,
                Name = name,
                CreatedAt = DateTime.UtcNow
            };
            
            _context.Sections.Add(section);
            await _context.SaveChangesAsync();
            
            // FIX: Return JSON instead of Redirect
            return Json(new { success = true, message = "Section added successfully!" });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSection(int id)
        {
            var section = await _context.Sections
                .Include(s => s.Batch)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (section == null)
            {
                return Json(new { success = false, message = "Section not found." });
            }

            var batchId = section.BatchId;

            try
            {
                _context.Sections.Remove(section);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error deleting section: {ex.Message}" });
            }
        }

        public async Task<IActionResult> EditSection(int id)
        {
            var section = await _context.Sections
                .Include(s => s.Batch)
                    .ThenInclude(b => b.Department)
                .Include(s => s.Students)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (section == null)
            {
                ShowMessage("Section not found!", "error");
                return RedirectToAction(nameof(Departments));
            }

            return View(section);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSection(int id, string name)
        {
            var section = await _context.Sections.FindAsync(id);
            if (section == null)
            {
                ShowMessage("Section not found!", "error");
                return RedirectToAction(nameof(Departments));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                ShowMessage("Section name is required!", "error");
                return RedirectToAction(nameof(EditSection), new { id });
            }

            section.Name = name;
            await _context.SaveChangesAsync();
            ShowMessage("Section updated successfully!");

            return RedirectToAction(nameof(EditBatch), new { id = section.BatchId });
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

        // ... existing code ...

        #region Student Management
        
        // UPDATE THIS METHOD
      public async Task<IActionResult> Students(int? departmentId, int? batchId, int? sectionId, string search)
        {
            // 1. Base Query
            var query = _context.Students
                .Include(s => s.Department)
                .Include(s => s.Batch)
                .Include(s => s.Section)
                .AsQueryable();

            // 2. Apply Filters
            if (departmentId.HasValue)
            {
                query = query.Where(s => s.DepartmentId == departmentId.Value);
            }

            if (batchId.HasValue)
            {
                query = query.Where(s => s.BatchId == batchId.Value);
            }

            if (sectionId.HasValue)
            {
                query = query.Where(s => s.SectionId == sectionId.Value);
            }

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(s => s.FullName.ToLower().Contains(search) 
                                      || s.RollNumber.ToLower().Contains(search) 
                                      || s.Email.ToLower().Contains(search));
            }

            var students = await query.OrderBy(s => s.RollNumber).ToListAsync();

            // 3. Populate Dropdowns (Preserve Selection)
            
            // Departments: Load all departments
            ViewData["Departments"] = new SelectList(
                await _context.Departments.OrderBy(d => d.Name).ToListAsync(), 
                "Id", "Name", departmentId);

            // Batches: Load distinct batches that have students, optionally filtered by department
            var batchesQuery = _context.Batches
                .Where(b => _context.Students.Any(s => s.BatchId == b.Id));
            
            if (departmentId.HasValue)
            {
                batchesQuery = batchesQuery.Where(b => _context.Students.Any(s => s.BatchId == b.Id && s.DepartmentId == departmentId.Value));
            }
            
            ViewData["Batches"] = new SelectList(
                await batchesQuery.Distinct().OrderByDescending(b => b.Year).ToListAsync(), 
                "Id", "Year", batchId);

            // Sections: Load sections based on selected batch and/or department
            var sectionsQuery = _context.Sections
                .Where(s => _context.Students.Any(st => st.SectionId == s.Id));
            
            if (batchId.HasValue)
            {
                sectionsQuery = sectionsQuery.Where(s => s.BatchId == batchId.Value);
            }
            else if (departmentId.HasValue)
            {
                // If no batch selected but department is, show sections from batches in that department
                sectionsQuery = sectionsQuery.Where(s => _context.Students.Any(st => st.SectionId == s.Id && st.DepartmentId == departmentId.Value));
            }
            
            ViewData["Sections"] = new SelectList(
                await sectionsQuery.Distinct().OrderBy(s => s.Name).ToListAsync(), 
                "Id", "Name", sectionId);

            // 4. Pass filter state to View for "No records" message
            ViewBag.CurrentDepartmentId = departmentId;
            ViewBag.CurrentBatchId = batchId;
            ViewBag.CurrentSectionId = sectionId;
            ViewBag.CurrentSearch = search;

            return View("~/Views/Admin/Students/Index.cshtml", students);
        }

      
      [HttpGet]
        public async Task<IActionResult> CreateStudent()
        {
            ViewData["DepartmentId"] = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name");
            
            var batches = await _context.Batches.ToListAsync();
            var sections = await _context.Sections.ToListAsync();
            
            ViewData["BatchId"] = new SelectList(batches, "Id", "Year");
            ViewData["SectionId"] = new SelectList(sections, "Id", "Name");
            
            return View("~/Views/Admin/CreateStudent.cshtml");
        }

       [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStudent(Student student)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // 1. Check if email already exists in the system
                    var existingUser = await _userManager.FindByEmailAsync(student.Email);
                    if (existingUser != null)
                    {
                        return Json(new { success = false, message = "A user with this email already exists." });
                    }

                    // 2. Create the Identity User (Login Account) first
                    var user = new ApplicationUser
                    {
                        UserName = student.Email,
                        Email = student.Email,
                        EmailConfirmed = true
                    };

                    // Set a default strong password
                    string defaultPassword = "Student@123"; 

                    var result = await _userManager.CreateAsync(user, defaultPassword);

                    if (result.Succeeded)
                    {
                        // 3. Assign the "Student" role
                        await _userManager.AddToRoleAsync(user, "Student");

                        // 4. Link the new User ID to the Student entity
                        student.UserId = user.Id; 
                        student.CreatedAt = DateTime.UtcNow;

                        // 5. Save the Student Record
                        _context.Students.Add(student);
                        await _context.SaveChangesAsync();
                    
                        return Json(new { 
                            success = true, 
                            message = $"Student created successfully! Default Password: {defaultPassword}" 
                        });
                    }
                    else
                    {
                        // If User creation failed
                        var errors = result.Errors.Select(e => e.Description).ToList();
                        return Json(new { 
                            success = false, 
                            message = "User creation failed", 
                            errors = errors 
                        });
                    }
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Error: {ex.Message}" });
                }
            }
            
            // Return validation errors as JSON
            var validationErrors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return Json(new { 
                success = false, 
                message = "Validation failed", 
                errors = validationErrors 
            });
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

        public IActionResult DownloadStudentTemplate()
        {
            try
            {
                var fileBytes = _excelService.GenerateStudentTemplate();
                var fileName = $"Student_Import_Template_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading student template");
                ShowMessage("Error generating template file.", "error");
                return RedirectToAction(nameof(UploadStudents));
            }
        }

        // Student Details
        public async Task<IActionResult> StudentDetails(int id)
        {
            var student = await _context.Students
                .Include(s => s.Department)
                .Include(s => s.Batch)
                .Include(s => s.Section)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (student == null)
            {
                ShowMessage("Student not found.", "error");
                return RedirectToAction(nameof(Students));
            }

            return View("~/Views/Admin/Students/Details.cshtml", student);
        }

        // Edit Student - GET
        public async Task<IActionResult> EditStudent(int id)
        {
            var student = await _context.Students
                .Include(s => s.Department)
                .Include(s => s.Batch)
                .Include(s => s.Section)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (student == null)
            {
                ShowMessage("Student not found.", "error");
                return RedirectToAction(nameof(Students));
            }

            await PopulateStudentDropdowns(student.DepartmentId, student.BatchId);
            return View("~/Views/Admin/Students/Edit.cshtml", student);
        }

        // Edit Student - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStudent(int id, Student student)
        {
            if (id != student.Id)
                return Json(new { success = false, message = "Invalid student ID." });

            if (ModelState.IsValid)
            {
                try
                {
                    var existingStudent = await _context.Students.FindAsync(id);
                    if (existingStudent == null)
                        return Json(new { success = false, message = "Student not found." });

                    // Update student properties
                    existingStudent.FullName = student.FullName;
                    existingStudent.Email = student.Email;
                    existingStudent.PhoneNumber = student.PhoneNumber;
                    existingStudent.DepartmentId = student.DepartmentId;
                    existingStudent.BatchId = student.BatchId;
                    existingStudent.SectionId = student.SectionId;

                    _context.Update(existingStudent);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Student updated successfully!" });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await StudentExists(student.Id))
                        return Json(new { success = false, message = "Student not found." });
                    return Json(new { success = false, message = "Error updating student. Please try again." });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Error: {ex.Message}" });
                }
            }

            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return Json(new { success = false, message = "Validation failed", errors = errors });
        }

        // Delete Student - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            try
            {
                var student = await _context.Students
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found." });
                }

                // Delete associated user account if exists
                if (student.User != null)
                {
                    var user = await _userManager.FindByIdAsync(student.UserId);
                    if (user != null)
                    {
                        await _userManager.DeleteAsync(user);
                    }
                }

                // Delete student record
                _context.Students.Remove(student);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Student deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error deleting student: {ex.Message}" });
            }
        }

        private async Task<bool> StudentExists(int id)
        {
            return await _context.Students.AnyAsync(e => e.Id == id);
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
            await LoadSelectedFilterNames(filters);
            
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
            await LoadSelectedFilterNames(filters);
            
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

                int totalClasses = 0;
                int markedClasses = 0;

                if (timetables.Any())
                {
                    // Get course IDs from timetables
                    var courseIds = timetables.Select(tt => tt.CourseId).Distinct().ToList();
                    
                    // Query attendance for each course separately to avoid Contains translation issues
                    var allAttendanceRecords = new List<(int CourseId, DateOnly Date, int Id)>();
                    
                    foreach (var courseId in courseIds)
                    {
                        var courseAttendances = await _context.Attendances
                            .Where(a => a.CourseId == courseId)
                            .Where(a => !filters.StartDate.HasValue || a.Date >= DateOnly.FromDateTime(filters.StartDate.Value))
                            .Where(a => !filters.EndDate.HasValue || a.Date <= DateOnly.FromDateTime(filters.EndDate.Value))
                            .Select(a => new { a.CourseId, a.Date, a.Id })
                            .ToListAsync();
                        
                        allAttendanceRecords.AddRange(courseAttendances.Select(a => (a.CourseId, a.Date, a.Id)));
                    }
                    
                    // Calculate metrics
                    totalClasses = allAttendanceRecords
                        .GroupBy(a => new { a.CourseId, a.Date })
                        .Count();
                    
                    markedClasses = allAttendanceRecords.Count;
                }

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
            await LoadSelectedFilterNames(filters);
            
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
            await LoadSelectedFilterNames(filters);
            
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
            await LoadSelectedFilterNames(filters);
            
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
                ShowMessage($"Error generating Excel report: {ex.Message}", "error");
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
                            ShowMessage("Student ID is required.", "error");
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
                ShowMessage($"Error generating PDF report: {ex.Message}", "error");
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

        private async Task LoadSelectedFilterNames(ReportFilterViewModel filters)
        {
            if (filters.DepartmentId.HasValue)
            {
                var department = await _context.Departments.FindAsync(filters.DepartmentId.Value);
                ViewBag.SelectedDepartment = department?.Name;
            }
            if (filters.BatchId.HasValue)
            {
                var batch = await _context.Batches.FindAsync(filters.BatchId.Value);
                ViewBag.SelectedBatch = batch?.Year;
            }
            if (filters.SectionId.HasValue)
            {
                var section = await _context.Sections.FindAsync(filters.SectionId.Value);
                ViewBag.SelectedSection = section?.Name;
            }
        }

        #endregion

        #region Utility Methods
        private bool DepartmentExists(int id)
        {
            return _context.Departments.Any(e => e.Id == id);
        }

        private async Task PopulateStudentDropdowns(int? departmentId = null, int? batchId = null)
        {
            ViewData["Departments"] = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name", departmentId);
            
            if (departmentId.HasValue)
            {
                var batches = await _context.Batches.Where(b => b.DepartmentId == departmentId.Value).ToListAsync();
                ViewData["Batches"] = new SelectList(batches, "Id", "Year", batchId);
            }
            else
            {
                ViewData["Batches"] = new SelectList(await _context.Batches.ToListAsync(), "Id", "Year", batchId);
            }

            if (batchId.HasValue)
            {
                var sections = await _context.Sections.Where(s => s.BatchId == batchId.Value).ToListAsync();
                ViewData["Sections"] = new SelectList(sections, "Id", "Name");
            }
            else
            {
                ViewData["Sections"] = new SelectList(await _context.Sections.ToListAsync(), "Id", "Name");
            }
            
            // Keep old keys for backward compatibility
            ViewData["DepartmentId"] = ViewData["Departments"];
            ViewData["BatchId"] = ViewData["Batches"];
            ViewData["SectionId"] = ViewData["Sections"];
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<JsonResult> GetBatchesByDepartment(int departmentId)
        {
            var batches = await _context.Batches
                .Where(b => b.DepartmentId == departmentId)
                .Select(b => new { b.Id, b.Year })
                .ToListAsync();
            return Json(batches);
        }

        [HttpGet]
        [AllowAnonymous]
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
                return Json(new { success = false, message = "Invalid Batch ID." });

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(batch);
                    await _context.SaveChangesAsync();
                    // FIX: Return JSON instead of Redirect
                    return Json(new { success = true, message = "Batch updated successfully!", departmentId = batch.DepartmentId });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Batches.Any(b => b.Id == batch.Id))
                        return Json(new { success = false, message = "Batch not found." });
                    throw;
                }
            }
            // Capture validation errors
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return Json(new { success = false, message = "Validation failed", errors = errors });
        }

        [HttpGet]
        public async Task<IActionResult> ViewBatch(int? id)
        {
            var batch = await _context.Batches
                .Include(b => b.Department)
                .Include(b => b.Sections)
                .Include(b => b.Timetables)
                // Fix: Add '!' after Students here as well if needed
                .Include(b => b.Students!)
                    .ThenInclude(s => s.Section)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (batch == null) return NotFound();

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
                try
                {
                    // Check if course code already exists
                    if (await _context.Courses.AnyAsync(c => c.Code == course.Code))
                    {
                        return Json(new { success = false, message = "A course with this code already exists." });
                    }

                    course.CreatedAt = DateTime.UtcNow;
                    _context.Courses.Add(course);
                    await _context.SaveChangesAsync();
                    
                    return Json(new { success = true, message = "Course created successfully!", courseId = course.Id });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Error: {ex.Message}" });
                }
            }
            
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return Json(new { success = false, message = "Validation failed", errors = errors });
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
                return Json(new { success = false, message = "Invalid course ID." });

            if (ModelState.IsValid)
            {
                try
                {
                    // Check if course code is being changed and already exists
                    if (await _context.Courses.AnyAsync(c => c.Code == course.Code && c.Id != course.Id))
                    {
                        return Json(new { success = false, message = "A course with this code already exists." });
                    }

                    _context.Update(course);
                    await _context.SaveChangesAsync();
                    
                    return Json(new { success = true, message = "Course updated successfully!" });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Courses.Any(e => e.Id == course.Id))
                        return Json(new { success = false, message = "Course not found." });
                    throw;
                }
            }
            
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return Json(new { success = false, message = "Validation failed", errors = errors });
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTeacher(Teacher teacher, string Password, string ConfirmPassword)
        {
            if (string.IsNullOrWhiteSpace(Password) || Password.Length < 6)
            {
                ModelState.AddModelError("Password", "Password must be at least 6 characters long.");
            }

            if (Password != ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Check if email already exists
                    var existingUser = await _userManager.FindByEmailAsync(teacher.Email);
                    if (existingUser != null)
                    {
                        ModelState.AddModelError("Email", "This email is already registered.");
                        ViewBag.Departments = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name", teacher.DepartmentId);
                        return View("~/Views/Admin/Teachers/AddTeacher.cshtml", teacher);
                    }

                    // Create user account
                    var user = new ApplicationUser
                    {
                        UserName = teacher.Email,
                        Email = teacher.Email,
                        EmailConfirmed = true
                    };

                    var result = await _userManager.CreateAsync(user, Password);
                    if (result.Succeeded)
                    {
                        // Assign Teacher role
                        await _userManager.AddToRoleAsync(user, "Teacher");

                        // Create teacher record
                        teacher.UserId = user.Id;
                        teacher.CreatedAt = DateTime.UtcNow;
                        teacher.IsApproved = true;
                        teacher.IsActive = true;

                        _context.Teachers.Add(teacher);
                        await _context.SaveChangesAsync();

                        ShowMessage("Teacher added successfully!");
                        return RedirectToAction("EditDepartment", new { id = teacher.DepartmentId });
                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error adding teacher");
                    ModelState.AddModelError(string.Empty, "An error occurred while adding the teacher.");
                }
            }

            ViewBag.Departments = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name", teacher.DepartmentId);
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
                return Json(new { success = false, message = "Invalid teacher ID." });

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(teacher);
                    await _context.SaveChangesAsync();
                    
                    return Json(new { 
                        success = true, 
                        message = "Teacher updated successfully!",
                        departmentId = teacher.DepartmentId 
                    });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Teachers.Any(t => t.Id == teacher.Id))
                        return Json(new { success = false, message = "Teacher not found." });
                    throw;
                }
            }
            
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return Json(new { success = false, message = "Validation failed", errors = errors });
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
                        return Json(new { success = false, message = "A user with this email already exists." });
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

                        return Json(new { 
                            success = true, 
                            message = "Teacher created successfully! Status: Pending Approval",
                            teacherId = teacher.Id,
                            action = action
                        });
                    }
                    else
                    {
                        var errors = result.Errors.Select(e => e.Description).ToList();
                        return Json(new { success = false, message = "User creation failed", errors = errors });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating teacher");
                    return Json(new { success = false, message = $"Error: {ex.Message}" });
                }
            }

            var validationErrors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return Json(new { success = false, message = "Validation failed", errors = validationErrors });
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
                    ShowMessage("Please add at least one course assignment.", "error");
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
                ShowMessage($"Error assigning courses: {ex.Message}", "error");
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
                ShowMessage($"Error removing assignment: {ex.Message}", "error");
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
                ShowMessage($"Error deleting teacher: {ex.Message}", "error");
                return RedirectToAction(nameof(Teachers));
            }
        }

        // Upload Teachers from Excel
        public IActionResult UploadTeachers()
        {
            ViewBag.Departments = new SelectList(_context.Departments.ToList(), "Id", "Name");
            return View("~/Views/Admin/Teachers/UploadTeachers.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadTeachers(IFormFile file, int departmentId)
        {
            if (file == null || file.Length == 0)
            {
                ShowMessage("Please select a valid Excel file.", "error");
                ViewBag.Departments = new SelectList(_context.Departments.ToList(), "Id", "Name");
                return View("~/Views/Admin/Teachers/UploadTeachers.cshtml");
            }

            if (!file.FileName.EndsWith(".xlsx") && !file.FileName.EndsWith(".xls"))
            {
                ShowMessage("Only Excel files (.xlsx, .xls) are allowed.", "error");
                ViewBag.Departments = new SelectList(_context.Departments.ToList(), "Id", "Name");
                return View("~/Views/Admin/Teachers/UploadTeachers.cshtml");
            }

            try
            {
                var teachers = await _excelService.ImportTeachersFromExcelAsync(file, departmentId);
                
                int successCount = 0;
                int errorCount = 0;
                var errors = new List<string>();

                foreach (var teacherData in teachers)
                {
                    try
                    {
                        // Check if email already exists
                        var existingUser = await _userManager.FindByEmailAsync(teacherData.Email);
                        if (existingUser != null)
                        {
                            errors.Add($"Row {teacherData.RowNumber}: Email {teacherData.Email} already exists.");
                            errorCount++;
                            continue;
                        }

                        // Check if employee ID already exists
                        var existingEmployee = await _context.Teachers
                            .FirstOrDefaultAsync(t => t.EmployeeId == teacherData.EmployeeId);
                        if (existingEmployee != null)
                        {
                            errors.Add($"Row {teacherData.RowNumber}: Employee ID {teacherData.EmployeeId} already exists.");
                            errorCount++;
                            continue;
                        }

                        // Create ApplicationUser
                        var user = new ApplicationUser
                        {
                            UserName = teacherData.Email,
                            Email = teacherData.Email,
                            EmailConfirmed = true
                        };

                        var result = await _userManager.CreateAsync(user, teacherData.Password);

                        if (result.Succeeded)
                        {
                            await _userManager.AddToRoleAsync(user, "Teacher");

                            // Create Teacher
                            var teacher = new Teacher
                            {
                                UserId = user.Id,
                                FullName = teacherData.FullName,
                                Email = teacherData.Email,
                                EmployeeId = teacherData.EmployeeId,
                                PhoneNumber = teacherData.PhoneNumber,
                                DepartmentId = departmentId,
                                IsApproved = false,
                                IsActive = true,
                                CreatedAt = DateTime.UtcNow
                            };

                            _context.Teachers.Add(teacher);
                            await _context.SaveChangesAsync();

                            successCount++;
                        }
                        else
                        {
                            errors.Add($"Row {teacherData.RowNumber}: Failed to create user - {string.Join(", ", result.Errors.Select(e => e.Description))}");
                            errorCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Row {teacherData.RowNumber}: {ex.Message}");
                        errorCount++;
                    }
                }

                if (successCount > 0)
                {
                    ShowMessage($"Successfully uploaded {successCount} teacher(s).");
                }

                if (errorCount > 0)
                {
                    var errorMessage = $"{errorCount} teacher(s) failed to upload:<br/>" + string.Join("<br/>", errors.Take(10));
                    if (errors.Count > 10)
                        errorMessage += $"<br/>... and {errors.Count - 10} more errors.";
                    
                    TempData["UploadErrors"] = errorMessage;
                }

                return RedirectToAction(nameof(Teachers));
            }
            catch (Exception ex)
            {
                ShowMessage($"Error processing Excel file: {ex.Message}", "error");
                ViewBag.Departments = new SelectList(_context.Departments.ToList(), "Id", "Name");
                return View("~/Views/Admin/Teachers/UploadTeachers.cshtml");
            }
        }

        [HttpGet]
        public IActionResult DownloadTeacherTemplate()
        {
            try
            {
                var fileBytes = _excelService.GenerateTeacherTemplate();
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Teacher_Upload_Template.xlsx");
            }
            catch (Exception ex)
            {
                ShowMessage($"Error generating template: {ex.Message}", "error");
                return RedirectToAction(nameof(Teachers));
            }
        }

        #endregion

        #region Student Summary

        public async Task<IActionResult> StudentSummary(int? courseId, int? batchId, int? sectionId, DateTime? fromDate, DateTime? toDate)
        {
            var viewModel = new StudentSummaryListViewModel
            {
                FromDate = fromDate ?? DateTime.Now.AddMonths(-1),
                ToDate = toDate ?? DateTime.Now,
                CourseId = courseId,
                BatchId = batchId,
                SectionId = sectionId
            };

            // Load filters
            viewModel.Courses = await _context.Courses
                .OrderBy(c => c.Title)
                .ToListAsync();

            viewModel.Batches = await _context.Batches
                .OrderBy(b => b.Year)
                .ToListAsync();

            if (batchId.HasValue)
            {
                viewModel.Sections = await _context.Sections
                    .Where(s => s.BatchId == batchId.Value)
                    .OrderBy(s => s.Name)
                    .ToListAsync();
            }

            // If filters are applied, get student data
            if (courseId.HasValue && batchId.HasValue && sectionId.HasValue)
            {
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

                    viewModel.Students.Add(new StudentSummaryItem
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
            var student = await _context.Students
                .Include(s => s.Batch)
                .Include(s => s.Section)
                .Include(s => s.Department)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (student == null)
            {
                ShowMessage("Student not found.", "error");
                return RedirectToAction(nameof(StudentSummary));
            }

            var viewModel = new StudentDetailViewModel
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

                    viewModel.AttendanceHistory.Add(new AttendanceHistoryItem
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
                // 1. Basic Validation: Start Time < End Time
                if (startTime >= endTime)
                {
                    ShowMessage("Start time must be before end time.", "error");
                    return RedirectToAction(nameof(ManageTimetable), new { departmentId, batchId, sectionId });
                }

                // 2. Validation: Teacher Conflict
                // Check if the selected teacher is already teaching another class during this time slot
                var teacherConflict = await _context.Timetables
                    .Include(t => t.Course)
                    .Include(t => t.Section)
                    .Where(t => t.TeacherId == teacherId 
                             && t.DayOfWeek == dayOfWeek
                             && ((t.StartTime < endTime) && (t.EndTime > startTime))) // Overlap logic
                    .FirstOrDefaultAsync();

                if (teacherConflict != null)
                {
                    var teacher = await _context.Teachers.FindAsync(teacherId);
                    ShowMessage($"Conflict: Teacher {teacher?.FullName} is already teaching {teacherConflict.Course?.Code} (Section {teacherConflict.Section?.Name}) at this time.", "error");
                    return RedirectToAction(nameof(ManageTimetable), new { departmentId, batchId, sectionId });
                }

                // 3. Validation: Section (Class) Conflict
                // Check if this specific section already has a lecture scheduled during this time slot
                var sectionConflict = await _context.Timetables
                    .Include(t => t.Course)
                    .Where(t => t.BatchId == batchId 
                             && t.SectionId == sectionId 
                             && t.DayOfWeek == dayOfWeek
                             && ((t.StartTime < endTime) && (t.EndTime > startTime))) // Overlap logic
                    .FirstOrDefaultAsync();

                if (sectionConflict != null)
                {
                    ShowMessage($"Conflict: This section already has a class ({sectionConflict.Course?.Code}) scheduled at this time.", "error");
                    return RedirectToAction(nameof(ManageTimetable), new { departmentId, batchId, sectionId });
                }

                // 4. No Conflicts: Create Entry
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

                ShowMessage("Class scheduled successfully!", "success");
            }
            catch (Exception ex)
            {
                ShowMessage($"Error scheduling class: {ex.Message}", "error");
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
                ShowMessage("Entry not found.", "error");
            }
            return RedirectToAction(nameof(ManageTimetable), new { departmentId, batchId, sectionId });
        }

        #endregion
    }
}