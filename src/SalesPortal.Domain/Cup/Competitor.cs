namespace SalesPortal.Domain.Cup
{
    public sealed class Competitor
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string PlayerId { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
        public decimal Matches { get; set; }
        public decimal Points { get; set; }
        public decimal GoalDifference { get; set; }
        public decimal TablePosition { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
