using AttendenceManagementSystem.Models;

namespace AttendenceManagementSystem.Services
{
    public interface IReportService
    {
        Task<byte[]> GenerateStudentAttendanceReportAsync(int studentId, DateOnly? fromDate = null, DateOnly? toDate = null);
        Task<byte[]> GenerateCourseAttendanceReportAsync(int courseId, DateOnly date);
        Task<byte[]> GenerateBatchAttendanceReportAsync(int batchId, int? courseId = null, DateOnly? fromDate = null, DateOnly? toDate = null);
    }
}