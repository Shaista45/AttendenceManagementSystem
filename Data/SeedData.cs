using AttendenceManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AttendenceManagementSystem.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Ensure database is created
            await context.Database.EnsureCreatedAsync();

            // Seed roles
            string[] roleNames = { "Admin", "Teacher", "Student" };
            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Create admin user
            var adminUser = await userManager.FindByEmailAsync("admin@university.com");
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = "admin@university.com",
                    Email = "admin@university.com",
                    FullName = "System Administrator",
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, "Admin123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");

                    // Seed sample data
                    await SeedSampleData(context);
                }
            }
        }

        private static async Task SeedSampleData(ApplicationDbContext context)
        {
            if (!await context.Departments.AnyAsync())
            {
                // Add departments
                var departments = new[]
                {
                    new Department { Name = "Computer Science", Code = "CS" },
                    new Department { Name = "Electrical Engineering", Code = "EE" },
                    new Department { Name = "Business Administration", Code = "BA" }
                };
                await context.Departments.AddRangeAsync(departments);
                await context.SaveChangesAsync();

                // Add batches
                var batches = new[]
                {
                    new Batch { Year = "2024", DepartmentId = 1 },
                    new Batch { Year = "2023", DepartmentId = 1 },
                    new Batch { Year = "2024", DepartmentId = 2 }
                };
                await context.Batches.AddRangeAsync(batches);
                await context.SaveChangesAsync();

                // Add sections
                var sections = new[]
                {
                    new Section { Name = "A", BatchId = 1 },
                    new Section { Name = "B", BatchId = 1 },
                    new Section { Name = "A", BatchId = 2 }
                };
                await context.Sections.AddRangeAsync(sections);
                await context.SaveChangesAsync();

                // Add courses
                var courses = new[]
                {
                    new Course { Code = "CS101", Title = "Introduction to Programming", DepartmentId = 1, Credits = 3 },
                    new Course { Code = "CS201", Title = "Data Structures", DepartmentId = 1, Credits = 4 },
                    new Course { Code = "EE101", Title = "Circuit Analysis", DepartmentId = 2, Credits = 3 }
                };
                await context.Courses.AddRangeAsync(courses);
                await context.SaveChangesAsync();
            }
        }
    }
}