namespace mvc_web.Models
{
    public class UserViewModel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Tc { get; set; } = "";
        public string SchoolNo { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Role { get; set; } = "Öğrenci";
        public string ClassName { get; set; } = "";
        public string LinkedStudentNo { get; set; } = "";
        public string Branch { get; set; } = "";
        public string ActivationCode { get; set; } = "";
        public bool MustChangePassword { get; set; } = true;
        public DateTime? CreatedAt { get; set; }
    }
}