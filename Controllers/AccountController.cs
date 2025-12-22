using AttendenceManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace AttendenceManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AttendenceManagementSystem.Data.ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            AttendenceManagementSystem.Data.ApplicationDbContext context,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> DiagnosticCheck()
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine("=== LOGIN DIAGNOSTIC CHECK ===\n");

            try
            {
                // Step 1: Check if admin user exists by email
                log.AppendLine("1. Checking admin user by email...");
                var adminByEmail = await _userManager.FindByEmailAsync("admin@university.com");
                if (adminByEmail != null)
                {
                    log.AppendLine($"   ✓ User found by email");
                    log.AppendLine($"   - User ID: {adminByEmail.Id}");
                    log.AppendLine($"   - UserName: {adminByEmail.UserName}");
                    log.AppendLine($"   - Email: {adminByEmail.Email}");
                    log.AppendLine($"   - Email Confirmed: {adminByEmail.EmailConfirmed}");
                    log.AppendLine($"   - Lockout Enabled: {adminByEmail.LockoutEnabled}");
                    log.AppendLine($"   - Lockout End: {adminByEmail.LockoutEnd}");
                    log.AppendLine($"   - Access Failed Count: {adminByEmail.AccessFailedCount}");
                }
                else
                {
                    log.AppendLine("   ✗ User NOT found by email!");
                }

                log.AppendLine();

                // Step 2: Check if admin user exists by username
                log.AppendLine("2. Checking admin user by username...");
                var adminByUsername = await _userManager.FindByNameAsync("admin@university.com");
                if (adminByUsername != null)
                {
                    log.AppendLine($"   ✓ User found by username");
                    log.AppendLine($"   - User ID: {adminByUsername.Id}");
                }
                else
                {
                    log.AppendLine("   ✗ User NOT found by username!");
                }

                log.AppendLine();

                // Step 3: Test password
                if (adminByEmail != null)
                {
                    log.AppendLine("3. Testing password 'Admin123!'...");
                    var passwordCheck = await _userManager.CheckPasswordAsync(adminByEmail, "Admin123!");
                    log.AppendLine($"   Password valid: {passwordCheck}");

                    if (!passwordCheck)
                    {
                        log.AppendLine("\n   Attempting to reset password...");
                        var token = await _userManager.GeneratePasswordResetTokenAsync(adminByEmail);
                        var resetResult = await _userManager.ResetPasswordAsync(adminByEmail, token, "Admin123!");
                        
                        if (resetResult.Succeeded)
                        {
                            log.AppendLine("   ✓ Password reset successful!");
                            
                            // Verify again
                            var recheckPassword = await _userManager.CheckPasswordAsync(adminByEmail, "Admin123!");
                            log.AppendLine($"   ✓ Password now validates: {recheckPassword}");
                        }
                        else
                        {
                            log.AppendLine($"   ✗ Password reset failed: {string.Join(", ", resetResult.Errors.Select(e => e.Description))}");
                        }
                    }
                }

                log.AppendLine();

                // Step 4: Check roles
                if (adminByEmail != null)
                {
                    log.AppendLine("4. Checking user roles...");
                    var roles = await _userManager.GetRolesAsync(adminByEmail);
                    if (roles.Any())
                    {
                        log.AppendLine($"   ✓ User has {roles.Count} role(s): {string.Join(", ", roles)}");
                    }
                    else
                    {
                        log.AppendLine("   ✗ User has NO roles!");
                        log.AppendLine("   Attempting to add Admin role...");
                        var addRoleResult = await _userManager.AddToRoleAsync(adminByEmail, "Admin");
                        if (addRoleResult.Succeeded)
                        {
                            log.AppendLine("   ✓ Admin role added successfully!");
                        }
                        else
                        {
                            log.AppendLine($"   ✗ Failed to add role: {string.Join(", ", addRoleResult.Errors.Select(e => e.Description))}");
                        }
                    }
                }

                log.AppendLine();

                // Step 5: Test actual sign-in
                if (adminByEmail != null)
                {
                    log.AppendLine("5. Testing PasswordSignInAsync...");
                    var signInResult = await _signInManager.PasswordSignInAsync(
                        adminByEmail.UserName ?? adminByEmail.Email ?? "",
                        "Admin123!",
                        false,
                        lockoutOnFailure: false
                    );

                    log.AppendLine($"   - Succeeded: {signInResult.Succeeded}");
                    log.AppendLine($"   - IsLockedOut: {signInResult.IsLockedOut}");
                    log.AppendLine($"   - IsNotAllowed: {signInResult.IsNotAllowed}");
                    log.AppendLine($"   - RequiresTwoFactor: {signInResult.RequiresTwoFactor}");

                    if (signInResult.Succeeded)
                    {
                        log.AppendLine("\n   ✓✓✓ SIGN IN SUCCESSFUL! ✓✓✓");
                        await _signInManager.SignOutAsync(); // Sign out after test
                        log.AppendLine("   (Signed out for testing purposes)");
                    }
                }

                log.AppendLine();
                log.AppendLine("=== DIAGNOSTIC CHECK COMPLETE ===");
                log.AppendLine("\nNow try logging in with:");
                log.AppendLine("Email: admin@university.com");
                log.AppendLine("Password: Admin123!");

            }
            catch (Exception ex)
            {
                log.AppendLine($"\n✗ ERROR: {ex.Message}");
                log.AppendLine($"Stack Trace: {ex.StackTrace}");
            }

            return Content(log.ToString(), "text/plain");
        }

        [HttpGet]
        public async Task<IActionResult> FixAdminPassword()
        {
            var admin = await _userManager.FindByEmailAsync("admin@university.com");
            if (admin == null)
                return Content("Admin user not found!");

            var token = await _userManager.GeneratePasswordResetTokenAsync(admin);
            var result = await _userManager.ResetPasswordAsync(admin, token, "Admin123!");

            if (result.Succeeded)
                return Content("SUCCESS! Admin password reset to 'Admin123!'. Now go back and try logging in.");
            else
                return Content($"Failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        [HttpGet]
        public async Task<IActionResult> InitializeDatabase()
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine("=== Database initialization started ===");

            try
            {
                await EnsureRolesExistAsync(log);
                var adminUser = await EnsureAdminUserAsync(log);
                await EnsureUserHasRoleAsync(adminUser, "Admin", log);
                await EnsureTeacherRecordExistsAsync(adminUser, log);

                log.AppendLine();
                log.AppendLine("Credentials");
                log.AppendLine("Email: admin@university.com");
                log.AppendLine("Password: Admin123!");
                log.AppendLine("=== Database initialization completed successfully ===");

                _logger.LogInformation("/Account/InitializeDatabase completed successfully");
                return Content(log.ToString(), "text/plain");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during database initialization");
                log.AppendLine();
                log.AppendLine($"✗ ERROR: {ex.Message}");
                return Content(log.ToString(), "text/plain");
            }
        }

        private async Task EnsureRolesExistAsync(System.Text.StringBuilder log)
        {
            string[] roleNames = { "Admin", "Teacher", "Student" };

            foreach (var roleName in roleNames)
            {
                if (await _roleManager.RoleExistsAsync(roleName))
                {
                    log.AppendLine($"• Role '{roleName}' already exists");
                    continue;
                }

                var roleResult = await _roleManager.CreateAsync(new IdentityRole(roleName));
                if (roleResult.Succeeded)
                {
                    log.AppendLine($"✓ Role '{roleName}' created successfully");
                    _logger.LogInformation("Role {RoleName} created", roleName);
                    continue;
                }

                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                log.AppendLine($"✗ Failed to create role '{roleName}': {errors}");
                throw new InvalidOperationException($"Failed to create role '{roleName}'");
            }
        }

        private async Task<ApplicationUser> EnsureAdminUserAsync(System.Text.StringBuilder log)
        {
            const string adminEmail = "admin@university.com";
            const string adminPassword = "Admin123!";

            var adminUser = await _userManager.FindByEmailAsync(adminEmail);
            if (adminUser != null)
            {
                log.AppendLine($"• Admin user already exists (ID: {adminUser.Id})");
                return adminUser;
            }

            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "System Administrator",
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(adminUser, adminPassword);
            if (createResult.Succeeded)
            {
                log.AppendLine("✓ Admin user created successfully");
                _logger.LogInformation("Admin user created with ID: {UserId}", adminUser.Id);
                return adminUser;
            }

            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            log.AppendLine($"✗ Failed to create admin user: {errors}");
            throw new InvalidOperationException("Failed to create admin user");
        }

        private async Task EnsureUserHasRoleAsync(ApplicationUser user, string roleName, System.Text.StringBuilder log)
        {
            if (await _userManager.IsInRoleAsync(user, roleName))
            {
                log.AppendLine($"• User already in '{roleName}' role");
                return;
            }

            var addRoleResult = await _userManager.AddToRoleAsync(user, roleName);
            if (addRoleResult.Succeeded)
            {
                log.AppendLine($"✓ User added to '{roleName}' role");
                _logger.LogInformation("User {UserId} added to role {RoleName}", user.Id, roleName);
                return;
            }

            var errors = string.Join(", ", addRoleResult.Errors.Select(e => e.Description));
            log.AppendLine($"✗ Failed to assign '{roleName}' role: {errors}");
            throw new InvalidOperationException($"Failed to assign role '{roleName}'");
        }

        private async Task EnsureTeacherRecordExistsAsync(ApplicationUser adminUser, System.Text.StringBuilder log)
        {
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == adminUser.Id);
            if (teacher != null)
            {
                log.AppendLine("• Teacher record already exists for admin user");
                return;
            }

            var department = await _context.Departments.FirstOrDefaultAsync();
            if (department == null)
            {
                department = new Department
                {
                    Name = "Administration",
                    Code = "ADMIN",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Departments.Add(department);
                await _context.SaveChangesAsync();
                log.AppendLine("✓ Default 'Administration' department created");
                _logger.LogInformation("Default Administration department created with ID {DepartmentId}", department.Id);
            }

            var newTeacher = new Teacher
            {
                UserId = adminUser.Id,
                FullName = string.IsNullOrWhiteSpace(adminUser.FullName) ? "System Administrator" : adminUser.FullName!,
                DepartmentId = department.Id,
                EmployeeId = "ADMIN001",
                Email = adminUser.Email,
                PhoneNumber = adminUser.PhoneNumber ?? "000-000-0000",
                CreatedAt = DateTime.UtcNow
            };

            _context.Teachers.Add(newTeacher);
            await _context.SaveChangesAsync();
            log.AppendLine("✓ Teacher record created for admin user");
            _logger.LogInformation("Teacher record created for admin user with ID {TeacherId}", newTeacher.Id);
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                var credential = model.Email?.Trim();
                _logger.LogInformation("Login attempt for credential: {Email}", credential);

                if (string.IsNullOrWhiteSpace(credential))
                {
                    ModelState.AddModelError(string.Empty, "Invalid email or password.");
                    return View(model);
                }

                // Resolve the actual user record so we always sign in with the correct username
                var user = await _userManager.FindByEmailAsync(credential);
                if (user == null)
                {
                    user = await _userManager.FindByNameAsync(credential);
                }

                if (user == null)
                {
                    _logger.LogWarning("No user found for credential: {Email}", credential);
                    ModelState.AddModelError(string.Empty, "Invalid email or password.");
                    return View(model);
                }
                
                // Use SignInManager's PasswordSignInAsync overload that accepts the user instance
                var result = await _signInManager.PasswordSignInAsync(
                    user,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: true
                );

                if (result.Succeeded)
                {
                    _logger.LogInformation("Login successful for user: {Email}", credential);
                    
                    // Redirect based on return URL or user role
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    
                    if (await _userManager.IsInRoleAsync(user, "Admin"))
                    {
                        _logger.LogInformation("Redirecting to Admin dashboard");
                        return RedirectToAction("Dashboard", "Admin");
                    }
                    else if (await _userManager.IsInRoleAsync(user, "Teacher"))
                    {
                        _logger.LogInformation("Redirecting to Teacher dashboard");
                        return RedirectToAction("Dashboard", "Teacher");
                    }
                    else if (await _userManager.IsInRoleAsync(user, "Student"))
                    {
                        _logger.LogInformation("Redirecting to Student dashboard");
                        return RedirectToAction("Dashboard", "Student");
                    }
                    
                    return RedirectToAction("Index", "Home");
                }
                
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account is locked out: {Email}", model.Email);
                    ModelState.AddModelError(string.Empty, "This account has been locked out due to multiple failed login attempts. Please try again later.");
                    return View(model);
                }
                
                if (result.IsNotAllowed)
                {
                    _logger.LogWarning("User is not allowed to sign in: {Email}", model.Email);
                    ModelState.AddModelError(string.Empty, "You are not allowed to sign in. Please confirm your email address.");
                    return View(model);
                }
                
                if (result.RequiresTwoFactor)
                {
                    _logger.LogInformation("Two-factor authentication required for: {Email}", model.Email);
                    return RedirectToAction("LoginWith2fa", new { returnUrl, model.RememberMe });
                }
                
                // Default: Invalid login attempt
                _logger.LogWarning("Invalid login attempt for: {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }
            else
            {
                _logger.LogWarning("ModelState is invalid for login attempt");
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            ViewData["Departments"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                await _context.Departments.ToListAsync(), "Id", "Name");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Validate that the selected Department, Batch, and Section exist and are properly linked
                var department = await _context.Departments.FindAsync(model.DepartmentId);
                var batch = await _context.Batches.FindAsync(model.BatchId);
                var section = await _context.Sections.FindAsync(model.SectionId);

                if (department == null)
                {
                    ModelState.AddModelError("DepartmentId", "Selected department does not exist.");
                }
                if (batch == null || batch.DepartmentId != model.DepartmentId)
                {
                    ModelState.AddModelError("BatchId", "Selected batch does not exist or does not belong to the selected department.");
                }
                if (section == null || section.BatchId != model.BatchId)
                {
                    ModelState.AddModelError("SectionId", "Selected section does not exist or does not belong to the selected batch.");
                }

                if (department == null || batch == null || section == null)
                {
                    // Repopulate dropdowns on error
                    ViewData["Departments"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                        await _context.Departments.ToListAsync(), "Id", "Name", model.DepartmentId);
                    ViewData["Batches"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                        await _context.Batches.Where(b => b.DepartmentId == model.DepartmentId).ToListAsync(), "Id", "Year", model.BatchId);
                    ViewData["Sections"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                        await _context.Sections.Where(s => s.BatchId == model.BatchId).ToListAsync(), "Id", "Name", model.SectionId);
                    return View(model);
                }

                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Assign to Student role by default
                    await _userManager.AddToRoleAsync(user, "Student");

                    // Create Student record with selected department, batch, and section
                    try
                    {
                        var rollNumber = model.RollNumber;
                        if (string.IsNullOrWhiteSpace(rollNumber))
                        {
                            rollNumber = $"S{new Random().Next(100000, 999999)}";
                        }

                        // Check if roll number already exists
                        if (await _context.Students.AnyAsync(s => s.RollNumber == rollNumber))
                        {
                            rollNumber = $"S{new Random().Next(100000, 999999)}";
                        }

                        var student = new AttendenceManagementSystem.Models.Student
                        {
                            UserId = user.Id,
                            FullName = model.FullName,
                            Email = model.Email,
                            RollNumber = rollNumber,
                            DepartmentId = model.DepartmentId,
                            BatchId = model.BatchId,
                            SectionId = model.SectionId,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Students.Add(student);
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        // Log the error but don't block registration
                        // The user account is created, they can be assigned to a department later by admin
                        System.Diagnostics.Debug.WriteLine($"Error creating student record: {ex.Message}");
                    }

                    await _signInManager.SignInAsync(user, isPersistent: false);

                    return RedirectToAction("Dashboard", "Student");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // Repopulate dropdowns on error
            ViewData["Departments"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                await _context.Departments.ToListAsync(), "Id", "Name", model.DepartmentId);
            ViewData["Batches"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                await _context.Batches.Where(b => b.DepartmentId == model.DepartmentId).ToListAsync(), "Id", "Year", model.BatchId);
            ViewData["Sections"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                await _context.Sections.Where(s => s.BatchId == model.BatchId).ToListAsync(), "Id", "Name", model.SectionId);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }

    public class LoginViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }

    public class RegisterViewModel
    {
        [Required]
        [StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Department")]
        public int DepartmentId { get; set; }

        [Required]
        [Display(Name = "Batch/Session")]
        public int BatchId { get; set; }

        [Required]
        [Display(Name = "Section")]
        public int SectionId { get; set; }

        [StringLength(50)]
        [Display(Name = "Roll Number")]
        public string? RollNumber { get; set; }
    }
}