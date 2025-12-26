using AttendenceManagementSystem.Models;

namespace AttendenceManagementSystem.Services
{
    public class TeacherExcelData
    {
        public int RowNumber { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public interface IExcelService
    {
        Task<(bool Success, string Message, int RecordsProcessed)> ImportStudentsFromExcelAsync(Stream fileStream, string fileName, string uploadedByUserId);
        Task<byte[]> ExportAttendanceToExcelAsync(int? courseId = null, DateOnly? fromDate = null, DateOnly? toDate = null);
        Task<List<TeacherExcelData>> ImportTeachersFromExcelAsync(IFormFile file, int departmentId);
        byte[] GenerateTeacherTemplate();
    }
}