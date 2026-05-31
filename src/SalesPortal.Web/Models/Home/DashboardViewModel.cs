namespace SalesPortal.Web.Models.Home
{
    public sealed class DashboardViewModel
    {
        public string UserName { get; set; } = string.Empty;
        public string RoleCode { get; set; } = string.Empty;
        public string RoleName => RoleCode == "A" ? "ADMIN" : "PLAYER";
        public string Team { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty;
        public string CountryName { get; set; } = string.Empty;
        public string CountryFlagFileName => string.IsNullOrWhiteSpace(CountryCode) ? string.Empty : $"{CountryCode}.png";
        public decimal PlayerPoints { get; set; }
        public decimal PlayerMatches { get; set; }
        public decimal PlayerGoalDifference { get; set; }
        public decimal GoalsFor { get; set; }
        public decimal GoalsAgainst { get; set; }
        public IReadOnlyList<StandingRowViewModel> Standings { get; set; } = Array.Empty<StandingRowViewModel>();
        public IReadOnlyList<TodayMatchViewModel> TodayMatches { get; set; } = Array.Empty<TodayMatchViewModel>();
        public IReadOnlyList<GoalChartRowViewModel> GoalChart { get; set; } = Array.Empty<GoalChartRowViewModel>();
    }

    public sealed class StandingRowViewModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string PlayerId { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty;
        public string CountryName { get; set; } = string.Empty;
        public string CountryFlagFileName => string.IsNullOrWhiteSpace(CountryCode) ? string.Empty : $"{CountryCode}.png";
        public decimal Matches { get; set; }
        public decimal Points { get; set; }
        public decimal GoalDifference { get; set; }
        public decimal Position { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal GoalsFor { get; set; }
        public decimal GoalsAgainst { get; set; }
        public bool IsCurrentUser { get; set; }
    }

    public sealed class TodayMatchViewModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string TimeLabel { get; set; } = string.Empty;
        public TodayMatchPlayerViewModel Player1 { get; set; } = new();
        public TodayMatchPlayerViewModel Player2 { get; set; } = new();
        public decimal? GoalsPlayer1 { get; set; }
        public decimal? GoalsPlayer2 { get; set; }
        public string ScoreLabel => GoalsPlayer1.HasValue || GoalsPlayer2.HasValue
            ? $"{GoalsPlayer1?.ToString("0") ?? "-"} - {GoalsPlayer2?.ToString("0") ?? "-"}"
            : "vs";
    }

    public sealed class TodayMatchPlayerViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string PlayerId { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty;
        public string CountryName { get; set; } = string.Empty;
        public string CountryFlagFileName => string.IsNullOrWhiteSpace(CountryCode) ? string.Empty : $"{CountryCode}.png";
    }

    public sealed class GoalChartRowViewModel
    {
        public string Team { get; set; } = string.Empty;
        public decimal GoalsFor { get; set; }
        public int Percentage { get; set; }
    }
}
