namespace mvc_web.Models
{
    public class TeacherAnnouncementViewModel
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string Author { get; set; } = "";
        public string Target { get; set; } = "";
        public DateTime? CreatedAt { get; set; }
    }
}