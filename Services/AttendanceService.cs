using AttendenceManagementSystem.Data;
using AttendenceManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AttendenceManagementSystem.Services
{
    public class AttendanceService : IAttendanceService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AttendanceService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<bool> MarkAttendanceAsync(int studentId, int courseId, DateOnly date, AttendanceStatus status, string markedByUserId, AttendanceSource source)
        {
            try
            {
                // Check if attendance already exists
                var existingAttendance = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.StudentId == studentId && a.CourseId == courseId && a.Date == date);

                if (existingAttendance != null)
                {
                    // Update existing attendance if not locked
                    if (!existingAttendance.IsLocked)
                    {
                        existingAttendance.Status = status;
                        existingAttendance.MarkedByUserId = markedByUserId;
                        existingAttendance.MarkedAt = DateTime.UtcNow;
                        existingAttendance.Source = source;
                    }
                    else
                    {
                        return false; // Attendance is locked
                    }
                }
                else
                {
                    // Create new attendance
                    var attendance = new Attendance
                    {
                        StudentId = studentId,
                        CourseId = courseId,
                        Date = date,
                        Status = status,
                        MarkedByUserId = markedByUserId,
                        MarkedAt = DateTime.UtcNow,
                        Source = source,
                        IsLocked = !await CanEditAttendanceAsync(date)
                    };

                    await _context.Attendances.AddAsync(attendance);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> CanEditAttendanceAsync(DateOnly date)
        {
            await Task.CompletedTask; // Fix the async warning
            var editWindowDays = _configuration.GetValue<int>("AttendanceSettings:EditWindowDays", 2);
            var cutoffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-editWindowDays));
            return date >= cutoffDate;
        }

        public async Task<List<Attendance>> GetStudentAttendanceAsync(int studentId, int? courseId = null, DateOnly? fromDate = null, DateOnly? toDate = null)
        {
            var query = _context.Attendances
                .Include(a => a.Course)
                .Include(a => a.MarkedByUser)
                .Where(a => a.StudentId == studentId);

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

            return await query.OrderByDescending(a => a.Date).ThenBy(a => a.Course!.Code).ToListAsync();
        }

        public async Task<Dictionary<int, double>> GetStudentAttendancePercentageAsync(int studentId)
        {
            var attendances = await _context.Attendances
                .Where(a => a.StudentId == studentId)
                .GroupBy(a => a.CourseId)
                .Select(g => new
                {
                    CourseId = g.Key,
                    TotalClasses = g.Count(),
                    PresentClasses = g.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late)
                })
                .ToListAsync();

            return attendances.ToDictionary(
                x => x.CourseId,
                x => x.TotalClasses > 0 ? Math.Round((double)x.PresentClasses / x.TotalClasses * 100, 2) : 0
            );
        }

        public async Task<List<Attendance>> GetCourseAttendanceAsync(int courseId, DateOnly date)
        {
            return await _context.Attendances
                .Include(a => a.Student)
                .Where(a => a.CourseId == courseId && a.Date == date)
                .OrderBy(a => a.Student!.RollNumber)
                .ToListAsync();
        }

        public async Task<bool> LockOldAttendancesAsync()
        {
            try
            {
                var editWindowDays = _configuration.GetValue<int>("AttendanceSettings:EditWindowDays", 2);
                var lockDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-editWindowDays));

                var oldAttendances = await _context.Attendances
                    .Where(a => a.Date <= lockDate && !a.IsLocked)
                    .ToListAsync();

                foreach (var attendance in oldAttendances)
                {
                    attendance.IsLocked = true;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> AutoMarkAttendanceAsync()
        {
            try
            {
                var currentTime = DateTime.UtcNow.TimeOfDay;
                var currentDay = DateTime.UtcNow.DayOfWeek;
                var today = DateOnly.FromDateTime(DateTime.UtcNow);

                // Get ongoing classes
                var ongoingClasses = await _context.Timetables
                    .Include(t => t.Course)
                    .Include(t => t.Batch)
                    .Include(t => t.Section)
                    .Where(t => t.DayOfWeek == currentDay &&
                               t.StartTime <= currentTime &&
                               t.EndTime >= currentTime)
                    .ToListAsync();

                foreach (var timetable in ongoingClasses)
                {
                    // Get enrolled students
                    var enrolledStudents = await _context.Enrollments
                        .Include(e => e.Student)
                        .Where(e => e.CourseId == timetable.CourseId &&
                                   e.Student!.BatchId == timetable.BatchId &&
                                   e.Student!.SectionId == timetable.SectionId)
                        .Select(e => e.Student)
                        .ToListAsync();

                    // Mark absent by default (will be updated when students log in)
                    foreach (var student in enrolledStudents)
                    {
                        if (student != null)
                        {
                            await MarkAttendanceAsync(
                                student.Id,
                                timetable.CourseId,
                                today,
                                AttendanceStatus.Absent,
                                "system",
                                AttendanceSource.Auto
                            );
                        }
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}