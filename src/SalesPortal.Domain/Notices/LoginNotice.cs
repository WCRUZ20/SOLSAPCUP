namespace SalesPortal.Domain.Notices
{
    public sealed class LoginNotice
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int DurationSeconds { get; set; } = 8;
        public bool IsActive { get; set; } = true;
    }
}
