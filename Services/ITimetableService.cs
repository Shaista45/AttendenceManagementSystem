using AttendenceManagementSystem.Models;

namespace AttendenceManagementSystem.Services
{
    public interface ITimetableService
    {
        Task<List<Timetable>> GetTeacherTimetableAsync(int teacherId);
        Task<List<Timetable>> GetStudentTimetableAsync(int studentId);
        Task<bool> IsClassOngoingAsync(int studentId);
        Task<Timetable?> GetCurrentClassAsync(int studentId);
    }
}