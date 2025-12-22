using AttendenceManagementSystem.Data;
using AttendenceManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendenceManagementSystem.Services
{
    public class TimetableService : ITimetableService
    {
        private readonly ApplicationDbContext _context;

        public TimetableService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Timetable>> GetTeacherTimetableAsync(int teacherId)
        {
            return await _context.Timetables
                .Include(t => t.Course)
                .Include(t => t.Batch)
                .Include(t => t.Section)
                .Where(t => t.TeacherId == teacherId)
                .OrderBy(t => t.DayOfWeek)
                .ThenBy(t => t.StartTime)
                .ToListAsync();
        }

        public async Task<List<Timetable>> GetStudentTimetableAsync(int studentId)
        {
            var student = await _context.Students
                .Include(s => s.Batch)
                .Include(s => s.Section)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null)
                return new List<Timetable>();

            return await _context.Timetables
                .Include(t => t.Course)
                .Include(t => t.Teacher)
                .Where(t => t.BatchId == student.BatchId && t.SectionId == student.SectionId)
                .OrderBy(t => t.DayOfWeek)
                .ThenBy(t => t.StartTime)
                .ToListAsync();
        }

        public async Task<bool> IsClassOngoingAsync(int studentId)
        {
            var currentClass = await GetCurrentClassAsync(studentId);
            return currentClass != null;
        }

        public async Task<Timetable?> GetCurrentClassAsync(int studentId)
        {
            var student = await _context.Students
                .Include(s => s.Batch)
                .Include(s => s.Section)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null)
                return null;

            var currentTime = DateTime.UtcNow.TimeOfDay;
            var currentDay = DateTime.UtcNow.DayOfWeek;

            return await _context.Timetables
                .Include(t => t.Course)
                .Include(t => t.Teacher)
                .FirstOrDefaultAsync(t => t.BatchId == student.BatchId &&
                                        t.SectionId == student.SectionId &&
                                        t.DayOfWeek == currentDay &&
                                        t.StartTime <= currentTime &&
                                        t.EndTime >= currentTime);
        }
    }
}