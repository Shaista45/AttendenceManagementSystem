using AttendenceManagementSystem.Models;

namespace AttendenceManagementSystem.Services
{
    public interface IAttendanceService
    {
        Task<bool> MarkAttendanceAsync(int studentId, int courseId, DateOnly date, AttendanceStatus status, string markedByUserId, AttendanceSource source);
        Task<bool> CanEditAttendanceAsync(DateOnly date);
        Task<List<Attendance>> GetStudentAttendanceAsync(int studentId, int? courseId = null, DateOnly? fromDate = null, DateOnly? toDate = null);
        Task<Dictionary<int, double>> GetStudentAttendancePercentageAsync(int studentId);
        Task<List<Attendance>> GetCourseAttendanceAsync(int courseId, DateOnly date);
        Task<bool> LockOldAttendancesAsync();
        Task<bool> AutoMarkAttendanceAsync();
    }
}