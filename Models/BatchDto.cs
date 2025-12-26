namespace AttendenceManagementSystem.Models
{
    public class BatchDto
    {
        public string Year { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<SectionDto>? Sections { get; set; }
    }

    public class SectionDto
    {
        public string Name { get; set; } = string.Empty;
        public List<int>? TeacherIds { get; set; }
    }
}
