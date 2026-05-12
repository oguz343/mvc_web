using Microsoft.AspNetCore.Mvc.Rendering;

namespace mvc_web.Models
{
    public class ClassViewModel
    {
        public string Id { get; set; } = "";

        public string Name { get; set; } = "";

        public int Grade { get; set; } = 9;

        public string Branch { get; set; } = "A";

        public string TeacherId { get; set; } = "";

        public string TeacherName { get; set; } = "";

        // Eski Classes/Index.cshtml bunu arıyor diye bıraktık.
        public string Teacher
        {
            get => TeacherName;
            set => TeacherName = value ?? "";
        }

        // Eski Classes/Index.cshtml bunu arıyor diye bıraktık.
        public string TeacherBranch { get; set; } = "";

        public int Capacity { get; set; } = 30;

        public int StudentCount { get; set; } = 0;

        public List<SelectListItem> Teachers { get; set; } = new();
    }
}