using AttendenceManagementSystem.Data;
using AttendenceManagementSystem.Models;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AttendenceManagementSystem.Services
{
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConverter _pdfConverter;

        public ReportService(ApplicationDbContext context, IConverter pdfConverter)
        {
            _context = context;
            _pdfConverter = pdfConverter;
        }

        public async Task<byte[]> GenerateStudentAttendanceReportAsync(int studentId, DateOnly? fromDate = null, DateOnly? toDate = null)
        {
            var student = await _context.Students
                .Include(s => s.Department)
                .Include(s => s.Batch)
                .Include(s => s.Section)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null)
                throw new ArgumentException("Student not found");

            var attendances = await _context.Attendances
                .Include(a => a.Course)
                .Where(a => a.StudentId == studentId)
                .OrderBy(a => a.Date)
                .ThenBy(a => a.Course.Code)
                .ToListAsync();

            var attendancePercentage = await _context.Attendances
                .Where(a => a.StudentId == studentId)
                .GroupBy(a => a.CourseId)
                .Select(g => new
                {
                    CourseId = g.Key,
                    TotalClasses = g.Count(),
                    PresentClasses = g.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late),
                    Percentage = g.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late) * 100.0 / g.Count()
                })
                .ToListAsync();

            var htmlContent = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; margin: 20px; }}
                        .header {{ text-align: center; margin-bottom: 30px; }}
                        .student-info {{ margin-bottom: 20px; }}
                        .table {{ width: 100%; border-collapse: collapse; margin-bottom: 20px; }}
                        .table th, .table td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
                        .table th {{ background-color: #f2f2f2; }}
                        .summary {{ margin-top: 30px; }}
                        .percentage {{ font-weight: bold; color: #2c3e50; }}
                    </style>
                </head>
                <body>
                    <div class='header'>
                        <h1>Student Attendance Report</h1>
                        <h2>{student.FullName}</h2>
                    </div>
                    
                    <div class='student-info'>
                        <p><strong>Roll Number:</strong> {student.RollNumber}</p>
                        <p><strong>Department:</strong> {student.Department.Name}</p>
                        <p><strong>Batch:</strong> {student.Batch.Year}</p>
                        <p><strong>Section:</strong> {student.Section.Name}</p>
                    </div>

                    <table class='table'>
                        <thead>
                            <tr>
                                <th>Date</th>
                                <th>Course</th>
                                <th>Status</th>
                            </tr>
                        </thead>
                        <tbody>";

            foreach (var attendance in attendances)
            {
                htmlContent += $@"
                    <tr>
                        <td>{attendance.Date:yyyy-MM-dd}</td>
                        <td>{attendance.Course.Code} - {attendance.Course.Title}</td>
                        <td>{attendance.Status}</td>
                    </tr>";
            }

            htmlContent += @"
                        </tbody>
                    </table>

                    <div class='summary'>
                        <h3>Attendance Summary</h3>
                        <table class='table'>
                            <thead>
                                <tr>
                                    <th>Course</th>
                                    <th>Total Classes</th>
                                    <th>Present Classes</th>
                                    <th>Percentage</th>
                                </tr>
                            </thead>
                            <tbody>";

            foreach (var course in attendancePercentage)
            {
                var courseInfo = await _context.Courses.FindAsync(course.CourseId);
                htmlContent += $@"
                    <tr>
                        <td>{courseInfo?.Code} - {courseInfo?.Title}</td>
                        <td>{course.TotalClasses}</td>
                        <td>{course.PresentClasses}</td>
                        <td class='percentage'>{course.Percentage:F2}%</td>
                    </tr>";
            }

            htmlContent += @"
                            </tbody>
                        </table>
                    </div>
                </body>
                </html>";

            return GeneratePdf(htmlContent);
        }

        public async Task<byte[]> GenerateCourseAttendanceReportAsync(int courseId, DateOnly date)
        {
            var course = await _context.Courses
                .Include(c => c.Department)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null)
                throw new ArgumentException("Course not found");

            var attendances = await _context.Attendances
                .Include(a => a.Student)
                .ThenInclude(s => s.Batch)
                .Include(a => a.Student)
                .ThenInclude(s => s.Section)
                .Where(a => a.CourseId == courseId && a.Date == date)
                .OrderBy(a => a.Student.RollNumber)
                .ToListAsync();

            var presentCount = attendances.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late);
            var totalCount = attendances.Count;
            var percentage = totalCount > 0 ? (presentCount * 100.0 / totalCount) : 0;

            var htmlContent = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; margin: 20px; }}
                        .header {{ text-align: center; margin-bottom: 30px; }}
                        .course-info {{ margin-bottom: 20px; }}
                        .table {{ width: 100%; border-collapse: collapse; margin-bottom: 20px; }}
                        .table th, .table td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
                        .table th {{ background-color: #f2f2f2; }}
                        .summary {{ margin-top: 30px; text-align: center; }}
                        .stat {{ font-size: 18px; margin: 10px 0; }}
                    </style>
                </head>
                <body>
                    <div class='header'>
                        <h1>Course Attendance Report</h1>
                        <h2>{course.Code} - {course.Title}</h2>
                        <h3>Date: {date:yyyy-MM-dd}</h3>
                    </div>
                    
                    <div class='course-info'>
                        <p><strong>Department:</strong> {course.Department.Name}</p>
                    </div>

                    <table class='table'>
                        <thead>
                            <tr>
                                <th>Roll Number</th>
                                <th>Student Name</th>
                                <th>Batch</th>
                                <th>Section</th>
                                <th>Status</th>
                            </tr>
                        </thead>
                        <tbody>";

            foreach (var attendance in attendances)
            {
                htmlContent += $@"
                    <tr>
                        <td>{attendance.Student.RollNumber}</td>
                        <td>{attendance.Student.FullName}</td>
                        <td>{attendance.Student.Batch.Year}</td>
                        <td>{attendance.Student.Section.Name}</td>
                        <td>{attendance.Status}</td>
                    </tr>";
            }

            htmlContent += $@"
                        </tbody>
                    </table>

                    <div class='summary'>
                        <div class='stat'><strong>Total Students:</strong> {totalCount}</div>
                        <div class='stat'><strong>Present Students:</strong> {presentCount}</div>
                        <div class='stat'><strong>Attendance Percentage:</strong> {percentage:F2}%</div>
                    </div>
                </body>
                </html>";

            return GeneratePdf(htmlContent);
        }

        public async Task<byte[]> GenerateBatchAttendanceReportAsync(int batchId, int? courseId = null, DateOnly? fromDate = null, DateOnly? toDate = null)
        {
            var batch = await _context.Batches
                .Include(b => b.Department)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null)
                throw new ArgumentException("Batch not found");

            // Implementation for batch report
            // Similar to above methods but aggregated for the entire batch

            var htmlContent = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; margin: 20px; }}
                        .header {{ text-align: center; margin-bottom: 30px; }}
                    </style>
                </head>
                <body>
                    <div class='header'>
                        <h1>Batch Attendance Report</h1>
                        <h2>Batch {batch.Year} - {batch.Department.Name}</h2>
                    </div>
                    <p>Batch attendance summary report implementation...</p>
                </body>
                </html>";

            return GeneratePdf(htmlContent);
        }

        private byte[] GeneratePdf(string htmlContent)
        {
            var doc = new HtmlToPdfDocument()
            {
                GlobalSettings = {
                    ColorMode = ColorMode.Color,
                    Orientation = Orientation.Portrait,
                    PaperSize = PaperKind.A4,
                    Margins = new MarginSettings { Top = 20, Bottom = 20, Left = 20, Right = 20 }
                },
                Objects = {
                    new ObjectSettings() {
                        PagesCount = true,
                        HtmlContent = htmlContent,
                        WebSettings = { DefaultEncoding = "utf-8" },
                        HeaderSettings = { FontSize = 9, Right = "Page [page] of [toPage]", Line = true },
                        FooterSettings = { FontSize = 9, Center = "Attendance Management System", Line = true }
                    }
                }
            };

            return _pdfConverter.Convert(doc);
        }
    }
}