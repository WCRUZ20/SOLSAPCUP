using System.ComponentModel.DataAnnotations;

namespace SalesPortal.Web.Models.Cup
{
    public sealed class MatchesViewModel
    {
        public IReadOnlyList<MatchRowViewModel> Matches { get; set; } = Array.Empty<MatchRowViewModel>();
    }

    public sealed class MatchRowViewModel
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

    public sealed class AdminCupPlayersViewModel
    {
        public IReadOnlyList<CompetitorFormViewModel> Competitors { get; set; } = Array.Empty<CompetitorFormViewModel>();
        public CompetitorFormViewModel Competitor { get; set; } = new();
    }

    public sealed class AdminCupMatchesViewModel
    {
        public IReadOnlyList<MatchFormViewModel> Matches { get; set; } = Array.Empty<MatchFormViewModel>();
        public MatchFormViewModel Match { get; set; } = new();
    }

    public sealed class CompetitorFormViewModel
    {
        [Required] public string Code { get; set; } = string.Empty;
        [Required] public string Name { get; set; } = string.Empty;
        public string PlayerId { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
        public decimal Matches { get; set; }
        public decimal Points { get; set; }
        public decimal GoalDifference { get; set; }
        public decimal TablePosition { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public sealed class MatchFormViewModel
    {
        [Required] public string Code { get; set; } = string.Empty;
        [Required] public string Name { get; set; } = string.Empty;
        public DateTime? MatchDate { get; set; }
        public short? MatchTime { get; set; }
        public string Player1 { get; set; } = string.Empty;
        public string Player2 { get; set; } = string.Empty;
        public decimal? GoalsPlayer1 { get; set; }
        public decimal? GoalsPlayer2 { get; set; }
        public string Observation { get; set; } = string.Empty;
    }
}
