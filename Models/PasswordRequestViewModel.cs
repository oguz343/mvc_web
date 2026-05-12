namespace mvc_web.Models
{
    public class PasswordRequestViewModel
    {
        public string Id { get; set; } = "";
        public string CollectionName { get; set; } = "";

        public string RequestKey { get; set; } = "";

        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Role { get; set; } = "";
        public string Number { get; set; } = "";
        public string Note { get; set; } = "";

        public string Status { get; set; } = "Bekliyor";
        public string ActivationCode { get; set; } = "";

        public DateTime? CreatedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? RejectedAt { get; set; }

        public bool IsPending =>
            Status == "Bekliyor" ||
            Status == "Pending" ||
            string.IsNullOrWhiteSpace(Status);
    }
}