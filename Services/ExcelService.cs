using AttendenceManagementSystem.Data;
using AttendenceManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ClosedXML.Excel;
using System.Drawing;
using Microsoft.AspNetCore.Http;

namespace AttendenceManagementSystem.Services
{
    public class ExcelService : IExcelService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ExcelService> _logger;

        public ExcelService(ApplicationDbContext context, ILogger<ExcelService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<(bool Success, string Message, int RecordsProcessed)> ImportStudentsFromExcelAsync(Stream fileStream, string fileName, string uploadedByUserId)
        {
            var recordsProcessed = 0;
            try
            {
                using var workbook = new XLWorkbook(fileStream);
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // Skip header

                foreach (var row in rows)
                {
                    try
                    {
                        var rollNumber = row.Cell(1).GetValue<string>();
                        var fullName = row.Cell(2).GetValue<string>();
                        var email = row.Cell(3).GetValue<string>();
                        var phoneNumber = row.Cell(4).GetValue<string>();
                        var departmentName = row.Cell(5).GetValue<string>();
                        var batchYear = row.Cell(6).GetValue<string>();
                        var sectionName = row.Cell(7).GetValue<string>();

                        // Find or create department
                        var department = await _context.Departments
                            .FirstOrDefaultAsync(d => d.Name == departmentName);
                        if (department == null)
                        {
                            department = new Department { Name = departmentName, Code = departmentName.Substring(0, 2).ToUpper() };
                            await _context.Departments.AddAsync(department);
                            await _context.SaveChangesAsync();
                        }

                        // Find or create batch
                        var batch = await _context.Batches
                            .FirstOrDefaultAsync(b => b.Year == batchYear && b.DepartmentId == department.Id);
                        if (batch == null)
                        {
                            batch = new Batch { Year = batchYear, DepartmentId = department.Id };
                            await _context.Batches.AddAsync(batch);
                            await _context.SaveChangesAsync();
                        }

                        // Find or create section
                        var section = await _context.Sections
                            .FirstOrDefaultAsync(s => s.Name == sectionName && s.BatchId == batch.Id);
                        if (section == null)
                        {
                            section = new Section { Name = sectionName, BatchId = batch.Id };
                            await _context.Sections.AddAsync(section);
                            await _context.SaveChangesAsync();
                        }

                        // Check if student already exists
                        var existingStudent = await _context.Students
                            .FirstOrDefaultAsync(s => s.RollNumber == rollNumber);

                        if (existingStudent == null)
                        {
                            // Create user for student
                            var user = new ApplicationUser
                            {
                                UserName = email,
                                Email = email,
                                FullName = fullName,
                                EmailConfirmed = true
                            };

                            // Note: In a real application, you would use UserManager to create the user
                            // and generate a temporary password
                            await _context.Users.AddAsync(user);
                            await _context.SaveChangesAsync();

                            // Create student record
                            var student = new Student
                            {
                                UserId = user.Id,
                                RollNumber = rollNumber,
                                FullName = fullName,
                                Email = email,
                                PhoneNumber = phoneNumber,
                                DepartmentId = department.Id,
                                BatchId = batch.Id,
                                SectionId = section.Id
                            };

                            await _context.Students.AddAsync(student);
                            await _context.SaveChangesAsync();

                            recordsProcessed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing row {RowNumber}", row.RowNumber());
                        continue;
                    }
                }

                // Log upload
                var uploadLog = new UploadLog
                {
                    FileName = fileName,
                    UploadedBy = uploadedByUserId,
                    UploadDate = DateTime.UtcNow,
                    Status = "Success",
                    RecordsProcessed = recordsProcessed
                };
                await _context.UploadLogs.AddAsync(uploadLog);
                await _context.SaveChangesAsync();

                return (true, $"Successfully imported {recordsProcessed} students", recordsProcessed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing students from Excel");

                // Log failed upload
                var uploadLog = new UploadLog
                {
                    FileName = fileName,
                    UploadedBy = uploadedByUserId,
                    UploadDate = DateTime.UtcNow,
                    Status = "Failed",
                    ErrorMessage = ex.Message,
                    RecordsProcessed = recordsProcessed
                };
                await _context.UploadLogs.AddAsync(uploadLog);
                await _context.SaveChangesAsync();

                return (false, $"Error importing students: {ex.Message}", recordsProcessed);
            }
        }

        public async Task<byte[]> ExportAttendanceToExcelAsync(int? courseId = null, DateOnly? fromDate = null, DateOnly? toDate = null)
        {
            try
            {
                var query = _context.Attendances
                    .Include(a => a.Student)
                    .Include(a => a.Course)
                    .Include(a => a.MarkedByUser)
                    .AsQueryable();

                if (courseId.HasValue)
                {
                    query = query.Where(a => a.CourseId == courseId.Value);
                }

                if (fromDate.HasValue)
                {
                    query = query.Where(a => a.Date >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(a => a.Date <= toDate.Value);
                }

                var attendances = await query
                    .OrderBy(a => a.Date)
                    .ThenBy(a => a.Course.Code)
                    .ThenBy(a => a.Student.RollNumber)
                    .ToListAsync();

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Attendance Report");

                // Headers
                worksheet.Cell(1, 1).Value = "Date";
                worksheet.Cell(1, 2).Value = "Roll Number";
                worksheet.Cell(1, 3).Value = "Student Name";
                worksheet.Cell(1, 4).Value = "Course Code";
                worksheet.Cell(1, 5).Value = "Course Title";
                worksheet.Cell(1, 6).Value = "Status";
                worksheet.Cell(1, 7).Value = "Marked By";
                worksheet.Cell(1, 8).Value = "Marked At";

                // Data
                var row = 2;
                foreach (var attendance in attendances)
                {
                    worksheet.Cell(row, 1).Value = attendance.Date.ToString("yyyy-MM-dd");
                    worksheet.Cell(row, 2).Value = attendance.Student.RollNumber;
                    worksheet.Cell(row, 3).Value = attendance.Student.FullName;
                    worksheet.Cell(row, 4).Value = attendance.Course.Code;
                    worksheet.Cell(row, 5).Value = attendance.Course.Title;
                    worksheet.Cell(row, 6).Value = attendance.Status.ToString();
                    worksheet.Cell(row, 7).Value = attendance.MarkedByUser.FullName;
                    worksheet.Cell(row, 8).Value = attendance.MarkedAt.ToString("yyyy-MM-dd HH:mm");
                    row++;
                }

                // Format headers
                var headerRange = worksheet.Range(1, 1, 1, 8);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                worksheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting attendance to Excel");
                throw;
            }
        }

        public async Task<List<TeacherExcelData>> ImportTeachersFromExcelAsync(IFormFile file, int departmentId)
        {
            var teachers = new List<TeacherExcelData>();

            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // Skip header

                int rowNumber = 1;
                foreach (var row in rows)
                {
                    rowNumber++;
                    try
                    {
                        var fullName = row.Cell(1).GetValue<string>().Trim();
                        var email = row.Cell(2).GetValue<string>().Trim();
                        var employeeId = row.Cell(3).GetValue<string>().Trim();
                        var phoneNumber = row.Cell(4).GetValue<string>().Trim();
                        var password = row.Cell(5).GetValue<string>().Trim();

                        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || 
                            string.IsNullOrWhiteSpace(employeeId) || string.IsNullOrWhiteSpace(password))
                        {
                            continue; // Skip empty rows
                        }

                        teachers.Add(new TeacherExcelData
                        {
                            RowNumber = rowNumber,
                            FullName = fullName,
                            Email = email,
                            EmployeeId = employeeId,
                            PhoneNumber = phoneNumber,
                            Password = password
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error reading row {rowNumber}: {ex.Message}");
                    }
                }

                return teachers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing teachers from Excel");
                throw new Exception($"Error reading Excel file: {ex.Message}");
            }
        }

        public byte[] GenerateTeacherTemplate()
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Teachers");

                // Add headers
                worksheet.Cell(1, 1).Value = "Full Name";
                worksheet.Cell(1, 2).Value = "Email";
                worksheet.Cell(1, 3).Value = "Employee ID";
                worksheet.Cell(1, 4).Value = "Phone Number";
                worksheet.Cell(1, 5).Value = "Password";

                // Add sample data
                worksheet.Cell(2, 1).Value = "John Doe";
                worksheet.Cell(2, 2).Value = "john.doe@example.com";
                worksheet.Cell(2, 3).Value = "EMP001";
                worksheet.Cell(2, 4).Value = "1234567890";
                worksheet.Cell(2, 5).Value = "Teacher@123";

                worksheet.Cell(3, 1).Value = "Jane Smith";
                worksheet.Cell(3, 2).Value = "jane.smith@example.com";
                worksheet.Cell(3, 3).Value = "EMP002";
                worksheet.Cell(3, 4).Value = "0987654321";
                worksheet.Cell(3, 5).Value = "Teacher@456";

                // Format headers
                var headerRange = worksheet.Range(1, 1, 1, 5);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Add instructions
                worksheet.Cell(5, 1).Value = "Instructions:";
                worksheet.Cell(6, 1).Value = "1. Fill in teacher details starting from row 2";
                worksheet.Cell(7, 1).Value = "2. All fields are required";
                worksheet.Cell(8, 1).Value = "3. Email must be unique and valid";
                worksheet.Cell(9, 1).Value = "4. Employee ID must be unique";
                worksheet.Cell(10, 1).Value = "5. Password must be at least 6 characters";
                worksheet.Cell(11, 1).Value = "6. Delete sample rows before uploading";

                var instructionsRange = worksheet.Range(5, 1, 11, 1);
                instructionsRange.Style.Font.Italic = true;
                instructionsRange.Style.Font.FontColor = XLColor.DarkGray;

                worksheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating teacher template");
                throw;
            }
        }
    }
}