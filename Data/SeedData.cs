using AttendenceManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AttendenceManagementSystem.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // 1. Ensure database is created
            await context.Database.EnsureCreatedAsync();

            // 2. Seed roles
            string[] roleNames = { "Admin", "Teacher", "Student" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 3. Create admin user
            var adminUser = await userManager.FindByEmailAsync("admin@university.com");
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = "admin@university.com",
                    Email = "admin@university.com",
                    FullName = "System Administrator",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(adminUser, "Admin123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // 4. Seed basic Reference Data (Departments, Batches, Sections)
            await SeedReferenceData(context);

            // 5. Seed Specific Scenario for Sidra and Ijaz
            await SeedSpecificScenarioAsync(context);
        }

        private static async Task SeedReferenceData(ApplicationDbContext context)
        {
            if (!await context.Departments.AnyAsync())
            {
                // Add departments
                var departments = new[]
                {
                    new Department { Name = "Computer Science", Code = "CS", CreatedAt = DateTime.UtcNow },
                    new Department { Name = "Electrical Engineering", Code = "EE", CreatedAt = DateTime.UtcNow },
                    new Department { Name = "Business Administration", Code = "BA", CreatedAt = DateTime.UtcNow }
                };
                await context.Departments.AddRangeAsync(departments);
                await context.SaveChangesAsync();

                // Add batches (linked to CS department for now)
                var csDept = await context.Departments.FirstAsync(d => d.Code == "CS");
                var batches = new[]
                {
                    new Batch { Year = "2024", DepartmentId = csDept.Id, Description = "Fall 2024", CreatedAt = DateTime.UtcNow },
                    new Batch { Year = "2025", DepartmentId = csDept.Id, Description = "Fall 2025", CreatedAt = DateTime.UtcNow }
                };
                await context.Batches.AddRangeAsync(batches);
                await context.SaveChangesAsync();

                // Add sections
                var batch2025 = await context.Batches.FirstAsync(b => b.Year == "2025");
                var sections = new[]
                {
                    new Section { Name = "A", BatchId = batch2025.Id, CreatedAt = DateTime.UtcNow },
                    new Section { Name = "B", BatchId = batch2025.Id, CreatedAt = DateTime.UtcNow }
                };
                await context.Sections.AddRangeAsync(sections);
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedSpecificScenarioAsync(ApplicationDbContext context)
        {
            // 1. Locate the specific Student and Teacher
            var sidra = await context.Students
                .Include(s => s.Department)
                .Include(s => s.Batch)
                .Include(s => s.Section)
                .FirstOrDefaultAsync(s => s.FullName == "Sidra Ijaz");

            var ijaz = await context.Teachers
                .FirstOrDefaultAsync(t => t.FullName == "Ijaz Hussain");

            // Only proceed if both exist (as per user request)
            if (sidra == null || ijaz == null)
            {
                return; // or throw exception depending on preference
            }

            // 2. Ensure 6 Courses exist for Sidra's Department
            // We use a fixed Semester "FALL 2025" to match the dashboard filter
            string semester = "FALL 2025";
            
            var coursesToCreate = new List<(string Code, string Title, int Credits)>
            {
                ("CS301", "Visual Programming", 3),
                ("CS302", "Web Engineering", 3),
                ("CS303", "Operating Systems", 4),
                ("CS304", "Database Systems II", 3),
                ("CS305", "Artificial Intelligence", 3),
                ("CS306", "Computer Networks", 3)
            };

            var courses = new List<Course>();

            foreach (var c in coursesToCreate)
            {
                var existingCourse = await context.Courses
                    .FirstOrDefaultAsync(x => x.Code == c.Code && x.DepartmentId == sidra.DepartmentId);

                if (existingCourse == null)
                {
                    existingCourse = new Course
                    {
                        Code = c.Code,
                        Title = c.Title,
                        Credits = c.Credits,
                        DepartmentId = sidra.DepartmentId,
                        Semester = semester, // Critical for "Register Subjects" dropdown
                        CreatedAt = DateTime.UtcNow
                    };
                    context.Courses.Add(existingCourse);
                }
                // Ensure semester is set if it was null (legacy data fix)
                else if (string.IsNullOrEmpty(existingCourse.Semester))
                {
                    existingCourse.Semester = semester;
                }
                
                courses.Add(existingCourse);
            }
            await context.SaveChangesAsync(); // Save courses to get IDs

            // 3. Enroll Sidra in all 6 courses
            foreach (var course in courses)
            {
                if (!await context.Enrollments.AnyAsync(e => e.StudentId == sidra.Id && e.CourseId == course.Id))
                {
                    context.Enrollments.Add(new Enrollment
                    {
                        StudentId = sidra.Id,
                        CourseId = course.Id,
                        EnrolledAt = DateTime.UtcNow
                    });
                }
            }
            await context.SaveChangesAsync();

            // 4. Create Timetable: Teacher Ijaz teaches ALL 6 courses to Sidra's Section
            // We schedule them Mon-Sat at 09:00 AM to 10:30 AM
            var days = new[] 
            { 
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, 
                DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday 
            };

            for (int i = 0; i < courses.Count; i++)
            {
                var course = courses[i];
                var day = days[i % days.Length]; // Cycle through days
                var startTime = new TimeSpan(9, 0, 0);
                var endTime = new TimeSpan(10, 30, 0);

                // Check if slot is free for this Section
                var exists = await context.Timetables.AnyAsync(t => 
                    t.SectionId == sidra.SectionId && 
                    t.DayOfWeek == day && 
                    t.StartTime == startTime);

                if (!exists)
                {
                    context.Timetables.Add(new Timetable
                    {
                        CourseId = course.Id,
                        TeacherId = ijaz.Id,
                        BatchId = sidra.BatchId,
                        SectionId = sidra.SectionId,
                        DayOfWeek = day,
                        StartTime = startTime,
                        EndTime = endTime,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            await context.SaveChangesAsync();
        }
    }
}