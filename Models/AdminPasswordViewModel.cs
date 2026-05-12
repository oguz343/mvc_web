namespace mvc_web.Models
{
    public class AdminPasswordViewModel
    {
        public string CurrentPassword { get; set; } = "";
        public string NewPassword { get; set; } = "";
        public string RepeatPassword { get; set; } = "";
    }
}