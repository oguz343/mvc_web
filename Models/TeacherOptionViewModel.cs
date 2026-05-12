namespace mvc_web.Models
{
    public class TeacherOptionViewModel
    {
        public string Id { get; set; } = "";

        public string Name { get; set; } = "";

        public string Branch { get; set; } = "";

        public string Number { get; set; } = "";

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Branch))
                {
                    return $"{Name} • {Branch}";
                }

                return Name;
            }
        }
    }
}