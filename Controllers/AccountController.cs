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
                
                var result = await _signInManager.PasswordSignInAsync(
                    user,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: true
                );

                if (result.Succeeded)
                {
                    _logger.LogInformation("Login successful for user: {Email}", credential);
                    
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    
                    if (await _userManager.IsInRoleAsync(user, "Admin"))
                    {
                        return RedirectToAction("Dashboard", "Admin");
                    }
                    else if (await _userManager.IsInRoleAsync(user, "Teacher"))
                    {
                        return RedirectToAction("Dashboard", "Teacher");
                    }
                    else if (await _userManager.IsInRoleAsync(user, "Student"))
                    {
                        return RedirectToAction("Dashboard", "Student");
                    }
                    
                    return RedirectToAction("Index", "Home");
                }
                
                if (result.IsLockedOut)
                {
                    ModelState.AddModelError(string.Empty, "This account has been locked out.");
                    return View(model);
                }
                
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    // Generate Token
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                    // Create URL
                    var callbackUrl = Url.Action("ResetPassword", "Account", 
                        new { token, email = user.Email }, Request.Scheme);

                    // LOG THE URL FOR TESTING (Check your Output window in Visual Studio)
                    _logger.LogInformation("Password Reset Token for {Email}: {Url}", model.Email, callbackUrl);
                }

                // Don't reveal that the user does not exist or is not confirmed
                return RedirectToAction("ForgotPasswordConfirmation");
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string? token = null, string? email = null)
        {
            if (token == null || email == null)
            {
                ModelState.AddModelError("", "Invalid password reset token");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToAction("ResetPasswordConfirmation");
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction("ResetPasswordConfirmation");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
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
                // Validate Department
                var department = await _context.Departments.FindAsync(model.DepartmentId);
                if (department == null)
                {
                    ModelState.AddModelError("DepartmentId", "Selected department does not exist.");
                    ViewData["Departments"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                        await _context.Departments.ToListAsync(), "Id", "Name", model.DepartmentId);
                    return View(model);
                }

                // Role-specific validation
                if (model.Role == "Student")
                {
                    if (!model.BatchId.HasValue || !model.SectionId.HasValue)
                    {
                        ModelState.AddModelError("", "Batch and Section are required for student registration.");
                        ViewData["Departments"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                            await _context.Departments.ToListAsync(), "Id", "Name", model.DepartmentId);
                        return View(model);
                    }
                }
                else if (model.Role == "Teacher")
                {
                    if (string.IsNullOrWhiteSpace(model.EmployeeId))
                    {
                        ModelState.AddModelError("EmployeeId", "Employee ID is required for teacher registration.");
                        ViewData["Departments"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                            await _context.Departments.ToListAsync(), "Id", "Name", model.DepartmentId);
                        return View(model);
                    }
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
                    await _userManager.AddToRoleAsync(user, model.Role);

                    try
                    {
                        if (model.Role == "Student")
                        {
                            var rollNumber = model.RollNumber;
                            if (string.IsNullOrWhiteSpace(rollNumber)) rollNumber = $"S{new Random().Next(100000, 999999)}";

                            var student = new AttendenceManagementSystem.Models.Student
                            {
                                UserId = user.Id,
                                FullName = model.FullName,
                                Email = model.Email,
                                RollNumber = rollNumber,
                                DepartmentId = model.DepartmentId,
                                BatchId = model.BatchId!.Value,
                                SectionId = model.SectionId!.Value,
                                PhoneNumber = model.PhoneNumber,
                                CreatedAt = DateTime.UtcNow
                            };

                            _context.Students.Add(student);
                            await _context.SaveChangesAsync();

                            await _signInManager.SignInAsync(user, isPersistent: false);
                            return RedirectToAction("Dashboard", "Student");
                        }
                        else if (model.Role == "Teacher")
                        {
                            var teacher = new AttendenceManagementSystem.Models.Teacher
                            {
                                UserId = user.Id,
                                FullName = model.FullName,
                                Email = model.Email,
                                EmployeeId = model.EmployeeId!,
                                PhoneNumber = model.PhoneNumber,
                                DepartmentId = model.DepartmentId,
                                IsApproved = false,
                                IsActive = true,
                                CreatedAt = DateTime.UtcNow
                            };

                            _context.Teachers.Add(teacher);
                            await _context.SaveChangesAsync();

                            TempData["Message"] = "Registration successful! Your account is pending admin approval.";
                            return RedirectToAction("Login");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", "Error creating profile: " + ex.Message);
                    }
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            ViewData["Departments"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                await _context.Departments.ToListAsync(), "Id", "Name", model.DepartmentId);
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

    // --- View Models ---

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
        [Display(Name = "Register As")]
        public string Role { get; set; } = "Student";

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

        [Display(Name = "Batch/Session")]
        public int? BatchId { get; set; }

        [Display(Name = "Section")]
        public int? SectionId { get; set; }

        [StringLength(50)]
        [Display(Name = "Roll Number")]
        public string? RollNumber { get; set; }

        [StringLength(50)]
        [Display(Name = "Employee ID")]
        public string? EmployeeId { get; set; }

        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }
    }

    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string Token { get; set; } = string.Empty;
    }
}