using AttendenceManagementSystem.Models;

namespace AttendenceManagementSystem.Services
{
    public interface IExcelService
    {
        Task<(bool Success, string Message, int RecordsProcessed)> ImportStudentsFromExcelAsync(Stream fileStream, string fileName, string uploadedByUserId);
        Task<byte[]> ExportAttendanceToExcelAsync(int? courseId = null, DateOnly? fromDate = null, DateOnly? toDate = null);
    }
}