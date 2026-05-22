namespace mvc_web.Models
{
    public class PortalViewModel
    {
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Role { get; set; } = "";
        public string Number { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string LinkedStudentNo { get; set; } = "";
        public string LinkedStudentName { get; set; } = "";

        public List<PortalAnnouncementItem> Announcements { get; set; } = new();
        public List<PortalHomeworkItem> Homeworks { get; set; } = new();
    }

    public class PortalAnnouncementItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string Author { get; set; } = "";
        public string Target { get; set; } = "";
        public DateTime? CreatedAt { get; set; }
    }

    public class PortalHomeworkItem
    {
        public string Id { get; set; } = "";
        public string CollectionName { get; set; } = "homeworks";

        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string LessonName { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public DateTime? DueDate { get; set; }
        public string AttachmentFileName { get; set; } = "";
        public string AttachmentFileUrl { get; set; } = "";

        public bool IsSubmitted { get; set; }
        public string SubmissionText { get; set; } = "";
        public string SubmissionFileName { get; set; } = "";
        public string SubmissionFileUrl { get; set; } = "";
        public DateTime? SubmittedAt { get; set; }

        public string Grade { get; set; } = "";
        public string Feedback { get; set; } = "";
        public DateTime? EvaluatedAt { get; set; }
    }
}
