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
        public MatchScheduleFormViewModel Schedule { get; set; } = new();
    }

    public sealed class AdminCupCountriesViewModel
    {
        public IReadOnlyList<CountryFormViewModel> Countries { get; set; } = Array.Empty<CountryFormViewModel>();
        public CountryFormViewModel Country { get; set; } = new();
    }


    public sealed class AdminCupNoticesViewModel
    {
        public IReadOnlyList<NoticeFormViewModel> Notices { get; set; } = Array.Empty<NoticeFormViewModel>();
        public NoticeFormViewModel Notice { get; set; } = new();
    }

    public sealed class NoticeFormViewModel
    {
        [Required] public string Code { get; set; } = string.Empty;
        [Required] public string Title { get; set; } = string.Empty;
        [Required] public string Message { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        [Required] public DateTime? StartDate { get; set; } = DateTime.Today;
        [Required] public DateTime? EndDate { get; set; } = DateTime.Today;
        [Required]
        [Range(1, 300)]
        public int DurationSeconds { get; set; } = 8;
        public bool IsActive { get; set; } = true;
        public string StatusLabel => IsActive ? "Activo" : "Inactivo";
    }

    public sealed class CompetitorFormViewModel
    {
        [Required] public string Code { get; set; } = string.Empty;
        [Required] public string Name { get; set; } = string.Empty;
        public string PlayerId { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty;
        public string CountryName { get; set; } = string.Empty;
        public string CountryFlagFileName => string.IsNullOrWhiteSpace(CountryCode) ? string.Empty : $"{CountryCode}.png";
        public decimal Matches { get; set; }
        public decimal Points { get; set; }
        public decimal GoalDifference { get; set; }
        public decimal TablePosition { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public sealed class MatchScheduleFormViewModel
    {
        [Required] public DateTime? StartDate { get; set; } = DateTime.Today;
        [Required] public string StartTime { get; set; } = "17:00";
        [Required]
        [Range(1, 365)]
        public int IntervalDays { get; set; } = 1;
        [Required]
        [Range(1, 1440)]
        public int IntervalMinutes { get; set; } = 10;
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

    public sealed class CountryFormViewModel
    {
        public int? Code { get; set; }
        [Required] public string CountryCode { get; set; } = string.Empty;
        [Required] public string CountryName { get; set; } = string.Empty;
        public string CountryFlagFileName => string.IsNullOrWhiteSpace(CountryCode) ? string.Empty : $"{CountryCode}.png";
        public string Name { get; set; } = string.Empty;
    }
}
