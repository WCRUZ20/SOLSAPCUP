using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesPortal.Application.Abstractions.Persistence;
using SalesPortal.Domain.Cup;
using SalesPortal.Shared.Security;
using SalesPortal.Web.Models.Home;
using System.Security.Claims;

namespace SalesPortal.Web.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    private readonly ITenantResolver _tenantResolver;
    private readonly ICupRepository _cupRepository;

    public HomeController(ITenantResolver tenantResolver, ICupRepository cupRepository)
    {
        _tenantResolver = tenantResolver;
        _cupRepository = cupRepository;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var playerId = User.FindFirstValue(PortalClaimTypes.CardCode) ?? string.Empty;
        var role = User.FindFirstValue(PortalClaimTypes.Role) ?? "P";
        var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);

        var competitors = await _cupRepository.GetCompetitorsAsync(tenant, cancellationToken);
        var matches = await _cupRepository.GetMatchesAsync(tenant, cancellationToken);
        var countries = await _cupRepository.GetWorldCupCountriesAsync(tenant, cancellationToken);
        var currentCompetitor = competitors.FirstOrDefault(c => string.Equals(c.PlayerId, playerId, StringComparison.OrdinalIgnoreCase));
        var currentCountry = currentCompetitor == null ? null : FindCountryForTeam(countries, currentCompetitor.Team);
        var statistics = CalculateStandings(competitors, matches);
        var standingRows = statistics
            .Select(row =>
            {
                var country = FindCountryForTeam(countries, row.Competitor.Team);
                return new StandingRowViewModel
                {
                    Code = row.Competitor.Code,
                    Name = row.Competitor.Name,
                    PlayerId = row.Competitor.PlayerId,
                    Team = row.Competitor.Team,
                    CountryCode = country?.CountryCode.Trim() ?? string.Empty,
                    CountryName = country == null ? row.Competitor.Team : GetCountryDisplayName(country),
                    Matches = row.Matches,
                    Points = row.Points,
                    GoalDifference = row.GoalDifference,
                    Position = row.Position,
                    Status = row.Competitor.Status,
                    GoalsFor = row.GoalsFor,
                    GoalsAgainst = row.GoalsAgainst,
                    IsCurrentUser = string.Equals(row.Competitor.PlayerId, playerId, StringComparison.OrdinalIgnoreCase)
                };
            })
            .ToList();

        var currentStatistics = currentCompetitor == null
            ? null
            : statistics.FirstOrDefault(row => string.Equals(row.Competitor.Code, currentCompetitor.Code, StringComparison.OrdinalIgnoreCase));
        var maxGoals = standingRows.Count == 0 ? 0 : standingRows.Max(row => row.GoalsFor);

        var model = new DashboardViewModel
        {
            UserName = User.Identity?.Name ?? string.Empty,
            RoleCode = role,
            Team = currentCompetitor?.Team ?? "Sin equipo asignado",
            CountryCode = currentCountry?.CountryCode.Trim() ?? string.Empty,
            CountryName = currentCountry == null ? (currentCompetitor?.Team ?? string.Empty) : GetCountryDisplayName(currentCountry),
            PlayerPoints = currentStatistics?.Points ?? 0,
            PlayerMatches = currentStatistics?.Matches ?? 0,
            PlayerGoalDifference = currentStatistics?.GoalDifference ?? 0,
            GoalsFor = currentStatistics?.GoalsFor ?? 0,
            GoalsAgainst = currentStatistics?.GoalsAgainst ?? 0,
            Standings = standingRows,
            TodayMatches = BuildTodayMatches(matches, competitors, countries),
            GoalChart = standingRows.Select(row => new GoalChartRowViewModel
            {
                Team = row.Team,
                GoalsFor = row.GoalsFor,
                Percentage = maxGoals <= 0 ? 0 : Math.Max(8, (int)Math.Round(row.GoalsFor / maxGoals * 100))
            }).ToList()
        };

        return View(model);
    }


    private static IReadOnlyList<TodayMatchViewModel> BuildTodayMatches(
        IReadOnlyList<CupMatch> matches,
        IReadOnlyList<Competitor> competitors,
        IReadOnlyList<WorldCupCountry> countries)
    {
        var today = DateTime.Today;

        return matches
            .Where(match => match.MatchDate.HasValue && match.MatchDate.Value.Date == today)
            .OrderBy(match => match.MatchTime ?? short.MaxValue)
            .ThenBy(match => match.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(match => match.Code, StringComparer.OrdinalIgnoreCase)
            .Select(match => new TodayMatchViewModel
            {
                Code = match.Code,
                Name = match.Name,
                TimeLabel = FormatMatchTime(match.MatchTime),
                Player1 = BuildTodayMatchPlayer(match.Player1, competitors, countries),
                Player2 = BuildTodayMatchPlayer(match.Player2, competitors, countries),
                GoalsPlayer1 = match.GoalsPlayer1,
                GoalsPlayer2 = match.GoalsPlayer2
            })
            .ToList();
    }

    private static TodayMatchPlayerViewModel BuildTodayMatchPlayer(
        string matchPlayer,
        IReadOnlyList<Competitor> competitors,
        IReadOnlyList<WorldCupCountry> countries)
    {
        var competitor = competitors.FirstOrDefault(candidate => IsSamePlayer(matchPlayer, candidate));
        var country = competitor == null ? null : FindCountryForTeam(countries, competitor.Team);

        return new TodayMatchPlayerViewModel
        {
            Name = competitor?.Name ?? matchPlayer,
            PlayerId = competitor?.PlayerId ?? matchPlayer,
            Team = competitor?.Team ?? string.Empty,
            CountryCode = country?.CountryCode.Trim() ?? string.Empty,
            CountryName = country == null ? (competitor?.Team ?? string.Empty) : GetCountryDisplayName(country)
        };
    }

    private static string FormatMatchTime(short? matchTime)
    {
        if (!matchTime.HasValue)
            return "Hora por definir";

        var time = Math.Clamp(matchTime.Value, (short)0, (short)2359);
        return $"{time / 100:00}:{time % 100:00}";
    }

    private static WorldCupCountry? FindCountryForTeam(IReadOnlyList<WorldCupCountry> countries, string team)
    {
        if (string.IsNullOrWhiteSpace(team))
            return null;

        return countries.FirstOrDefault(country => GetCountryAssignmentKeys(country)
            .Any(key => string.Equals(key, team.Trim(), StringComparison.OrdinalIgnoreCase)));
    }

    private static IEnumerable<string> GetCountryAssignmentKeys(WorldCupCountry country)
    {
        if (!string.IsNullOrWhiteSpace(country.CountryName))
            yield return country.CountryName.Trim();

        if (!string.IsNullOrWhiteSpace(country.CountryCode))
            yield return country.CountryCode.Trim();

        if (!string.IsNullOrWhiteSpace(country.Name))
            yield return country.Name.Trim();
    }

    private static string GetCountryDisplayName(WorldCupCountry country)
    {
        return GetCountryAssignmentKeys(country).FirstOrDefault() ?? string.Empty;
    }

    private static IReadOnlyList<StandingStatistics> CalculateStandings(
        IReadOnlyList<Competitor> competitors,
        IReadOnlyList<CupMatch> matches)
    {
        var today = DateTime.Today;
        var statistics = competitors
            .Select(competitor => new StandingStatistics(competitor))
            .ToList();

        foreach (var match in matches.Where(match => IsPlayedMatch(match, today)))
        {
            var player1 = FindStatisticsForPlayer(match.Player1, statistics);
            var player2 = FindStatisticsForPlayer(match.Player2, statistics);

            if (player1 == null || player2 == null || ReferenceEquals(player1, player2))
                continue;

            var goalsPlayer1 = match.GoalsPlayer1.GetValueOrDefault();
            var goalsPlayer2 = match.GoalsPlayer2.GetValueOrDefault();

            player1.ApplyMatch(goalsPlayer1, goalsPlayer2);
            player2.ApplyMatch(goalsPlayer2, goalsPlayer1);
        }

        var position = 1;
        foreach (var row in statistics
            .OrderByDescending(row => row.Points)
            .ThenByDescending(row => row.GoalDifference)
            .ThenBy(row => row.Competitor.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Competitor.Code, StringComparer.OrdinalIgnoreCase))
        {
            row.Position = position++;
        }

        return statistics
            .OrderBy(row => row.Position)
            .ToList();
    }

    private static bool IsPlayedMatch(CupMatch match, DateTime today)
    {
        return match.MatchDate.HasValue
            && match.MatchDate.Value.Date <= today
            && match.GoalsPlayer1.HasValue
            && match.GoalsPlayer2.HasValue;
    }

    private static StandingStatistics? FindStatisticsForPlayer(string matchPlayer, IEnumerable<StandingStatistics> statistics)
    {
        if (string.IsNullOrWhiteSpace(matchPlayer))
            return null;

        return statistics.FirstOrDefault(row => IsSamePlayer(matchPlayer, row.Competitor));
    }

    private static bool IsSamePlayer(string matchPlayer, Competitor competitor)
    {
        return string.Equals(matchPlayer, competitor.PlayerId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(matchPlayer, competitor.Code, StringComparison.OrdinalIgnoreCase)
            || string.Equals(matchPlayer, competitor.Name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(matchPlayer, competitor.Team, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StandingStatistics
    {
        public StandingStatistics(Competitor competitor)
        {
            Competitor = competitor;
        }

        public Competitor Competitor { get; }
        public decimal Matches { get; private set; }
        public decimal Points { get; private set; }
        public decimal GoalDifference { get; private set; }
        public decimal GoalsFor { get; private set; }
        public decimal GoalsAgainst { get; private set; }
        public decimal Position { get; set; }

        public void ApplyMatch(decimal goalsFor, decimal goalsAgainst)
        {
            Matches++;
            GoalsFor += goalsFor;
            GoalsAgainst += goalsAgainst;
            GoalDifference += goalsFor - goalsAgainst;

            if (goalsFor > goalsAgainst)
                Points += 3;
            else if (goalsFor == goalsAgainst)
                Points += 1;
        }
    }
}
