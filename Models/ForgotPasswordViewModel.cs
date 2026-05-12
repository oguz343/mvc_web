namespace mvc_web.Models
{
    public class ForgotPasswordViewModel
    {
        public string Role { get; set; } = "";
        public string Name { get; set; } = "";
        public string Number { get; set; } = "";

        // Sadece not isteğe bağlı.
        public string? Note { get; set; }
    }
}