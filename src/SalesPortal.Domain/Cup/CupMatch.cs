namespace SalesPortal.Domain.Cup
{
    public sealed class CupMatch
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime? MatchDate { get; set; }
        public short? MatchTime { get; set; }
        public string Player1 { get; set; } = string.Empty;
        public string Player2 { get; set; } = string.Empty;
        public decimal? GoalsPlayer1 { get; set; }
        public decimal? GoalsPlayer2 { get; set; }
        public string Observation { get; set; } = string.Empty;
    }
}
