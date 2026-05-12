namespace mvc_web.Models
{
    public class TeacherSubmissionViewModel
    {
        public string Id { get; set; } = "";
        public string SubmissionCollection { get; set; } = "homework_submissions";

        public string HomeworkId { get; set; } = "";
        public string HomeworkCollection { get; set; } = "homeworks";

        public string HomeworkTitle { get; set; } = "";
        public string LessonName { get; set; } = "";
        public string ClassName { get; set; } = "";

        public string StudentId { get; set; } = "";
        public string StudentName { get; set; } = "";
        public string StudentNo { get; set; } = "";

        public string AnswerText { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FileUrl { get; set; } = "";

        public string Status { get; set; } = "Bekliyor";
        public string Grade { get; set; } = "";
        public string Feedback { get; set; } = "";

        public DateTime? SubmittedAt { get; set; }
        public DateTime? EvaluatedAt { get; set; }
    }
}