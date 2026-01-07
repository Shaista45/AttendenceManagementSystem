// Program.cs - Fixed with Complete Mock Converter
using Microsoft.EntityFrameworkCore;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.AspNetCore.Identity;
using AttendenceManagementSystem.Data;
using AttendenceManagementSystem.Models;
using AttendenceManagementSystem.Services;
using AutoMapper;
using System.Runtime.Loader;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    connectionString = "Server=MY-PC\\SQLEXPRESS;Database=AttendenceManagementSystem;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=true";
}

Console.WriteLine($"Using connection string: {connectionString}");

// Database configuration
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlServerOptions =>
    {
        sqlServerOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
        sqlServerOptions.CommandTimeout(120);
    });

    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
});

// Identity configuration
builder.Services.AddIdentity<AttendenceManagementSystem.Models.ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+"; // Allow email characters
    options.User.RequireUniqueEmail = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure cookie authentication for web pages
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(30); // Extended to 30 days
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    // Make cookie persistent across browser sessions
    options.Cookie.MaxAge = TimeSpan.FromDays(30);
});

// JWT Authentication for API endpoints
builder.Services.AddAuthentication()
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});

// MVC and Razor Pages Services
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Register custom services
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IExcelService, ExcelService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ITimetableService, TimetableService>();
builder.Services.AddScoped<AttendenceManagementSystem.Services.ISeedData, AttendenceManagementSystem.Services.SeedDataService>();
builder.Services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

// Register DinkToPdf converter
try
{
    // Try to load DinkToPdf
    var converter = new SynchronizedConverter(new PdfTools());
    builder.Services.AddSingleton(typeof(IConverter), converter);
    Console.WriteLine("DinkToPdf converter registered successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: Could not load DinkToPdf. PDF generation may not work: {ex.Message}");
    // Register a complete mock converter
    builder.Services.AddSingleton<IConverter>(new MockPdfConverter());
}

// AutoMapper - simplified registration
builder.Services.AddAutoMapper(typeof(Program));

// Logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Information);
});

// Session services
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// HTTP Context Accessor
builder.Services.AddHttpContextAccessor();

// Add Antiforgery services
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.FormFieldName = "__RequestVerificationToken";
    options.Cookie.Name = "X-CSRF-TOKEN";
    options.SuppressXFrameOptionsHeader = false;
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Map endpoints
app.MapRazorPages();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// Database initialization and seeding
try
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<ApplicationDbContext>();

    logger.LogInformation("Starting database initialization...");

    // Ensure database is created
    await context.Database.EnsureCreatedAsync();
    
    // Check if we need to apply migrations
    var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
    if (pendingMigrations.Any())
    {
        logger.LogInformation("Applying pending migrations...");
        await context.Database.MigrateAsync();
        logger.LogInformation("Migrations applied successfully.");
    }
    else
    {
        logger.LogInformation("No pending migrations.");
    }

    // Initialize database roles and basic structure
    var dbInitializer = services.GetRequiredService<AttendenceManagementSystem.Services.IDatabaseInitializer>();
    await dbInitializer.InitializeAsync();

    // Seed initial data
    var seedData = services.GetRequiredService<AttendenceManagementSystem.Services.ISeedData>();
    await seedData.InitializeAsync();

    // Ensure admin user exists
    logger.LogInformation("Ensuring admin user exists...");
    var userManager = services.GetRequiredService<UserManager<AttendenceManagementSystem.Models.ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    
    // Create Admin role if it doesn't exist
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }
    
    // Create Teacher role if it doesn't exist
    if (!await roleManager.RoleExistsAsync("Teacher"))
    {
        await roleManager.CreateAsync(new IdentityRole("Teacher"));
    }
    
    // Create Student role if it doesn't exist
    if (!await roleManager.RoleExistsAsync("Student"))
    {
        await roleManager.CreateAsync(new IdentityRole("Student"));
    }

    // Check if admin user exists
    var adminEmail = "admin@university.com";
    var admin = await userManager.FindByEmailAsync(adminEmail);
    
    if (admin == null)
    {
        logger.LogInformation("Creating admin user...");
        admin = new AttendenceManagementSystem.Models.ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            FullName = "System Administrator",
            PhoneNumber = "1234567890"
        };
        
        var createResult = await userManager.CreateAsync(admin, "Admin123!");
        if (createResult.Succeeded)
        {
            logger.LogInformation("Admin user created successfully.");
        }
        else
        {
            logger.LogError("Failed to create admin user: {Errors}",
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }
    }
    else
    {
        logger.LogInformation("Admin user already exists.");
    }
    
    // Ensure admin is in Admin role
    if (admin != null && !await userManager.IsInRoleAsync(admin, "Admin"))
    {
        await userManager.AddToRoleAsync(admin, "Admin");
        logger.LogInformation("Admin user added to Admin role.");
    }

    logger.LogInformation("Database initialization and seeding completed successfully.");
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred during application startup.");
    
    // Don't crash the app in development
    if (app.Environment.IsDevelopment())
    {
        logger.LogWarning("Continuing in development mode despite initialization errors.");
    }
}

// Run the application
app.Run();

// Complete Mock PDF Converter with all interface members
public class MockPdfConverter : IConverter
{
    public byte[] Convert(IDocument document)
    {
        throw new InvalidOperationException("PDF generation is not available. DinkToPdf library failed to load.");
    }

    // Implement all required interface members
    public event EventHandler<DinkToPdf.EventDefinitions.PhaseChangedArgs> PhaseChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<DinkToPdf.EventDefinitions.ProgressChangedArgs> ProgressChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<DinkToPdf.EventDefinitions.FinishedArgs> Finished
    {
        add { }
        remove { }
    }

    public event EventHandler<DinkToPdf.EventDefinitions.ErrorArgs> Error
    {
        add { }
        remove { }
    }

    public event EventHandler<DinkToPdf.EventDefinitions.WarningArgs> Warning
    {
        add { }
        remove { }
    }
}