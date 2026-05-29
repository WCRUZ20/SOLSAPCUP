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
        var standingRows = competitors
            .Select(competitor =>
            {
                var goals = CalculateGoals(competitor, matches);
                var country = FindCountryForTeam(countries, competitor.Team);
                return new StandingRowViewModel
                {
                    Code = competitor.Code,
                    Name = competitor.Name,
                    PlayerId = competitor.PlayerId,
                    Team = competitor.Team,
                    CountryCode = country?.CountryCode.Trim() ?? string.Empty,
                    CountryName = country == null ? competitor.Team : GetCountryDisplayName(country),
                    Matches = competitor.Matches,
                    Points = competitor.Points,
                    GoalDifference = competitor.GoalDifference,
                    Position = competitor.TablePosition,
                    Status = competitor.Status,
                    GoalsFor = goals.For,
                    GoalsAgainst = goals.Against,
                    IsCurrentUser = string.Equals(competitor.PlayerId, playerId, StringComparison.OrdinalIgnoreCase)
                };
            })
            .OrderBy(row => row.Position == 0 ? decimal.MaxValue : row.Position)
            .ThenByDescending(row => row.Points)
            .ThenByDescending(row => row.GoalDifference)
            .ToList();

        var currentGoals = currentCompetitor == null ? (For: 0m, Against: 0m) : CalculateGoals(currentCompetitor, matches);
        var maxGoals = standingRows.Count == 0 ? 0 : standingRows.Max(row => row.GoalsFor);

        var model = new DashboardViewModel
        {
            UserName = User.Identity?.Name ?? string.Empty,
            RoleCode = role,
            Team = currentCompetitor?.Team ?? "Sin equipo asignado",
            CountryCode = currentCountry?.CountryCode.Trim() ?? string.Empty,
            CountryName = currentCountry == null ? (currentCompetitor?.Team ?? string.Empty) : GetCountryDisplayName(currentCountry),
            PlayerPoints = currentCompetitor?.Points ?? 0,
            PlayerMatches = currentCompetitor?.Matches ?? 0,
            PlayerGoalDifference = currentCompetitor?.GoalDifference ?? 0,
            GoalsFor = currentGoals.For,
            GoalsAgainst = currentGoals.Against,
            Standings = standingRows,
            GoalChart = standingRows.Take(8).Select(row => new GoalChartRowViewModel
            {
                Team = row.Team,
                GoalsFor = row.GoalsFor,
                Percentage = maxGoals <= 0 ? 0 : Math.Max(8, (int)Math.Round(row.GoalsFor / maxGoals * 100))
            }).ToList()
        };

        return View(model);
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

    private static (decimal For, decimal Against) CalculateGoals(SalesPortal.Domain.Cup.Competitor competitor, IReadOnlyList<SalesPortal.Domain.Cup.CupMatch> matches)
    {
        var goalsFor = 0m;
        var goalsAgainst = 0m;
        foreach (var match in matches)
        {
            var isPlayer1 = IsSamePlayer(match.Player1, competitor);
            var isPlayer2 = IsSamePlayer(match.Player2, competitor);
            if (!isPlayer1 && !isPlayer2)
                continue;

            if (isPlayer1)
            {
                goalsFor += match.GoalsPlayer1 ?? 0;
                goalsAgainst += match.GoalsPlayer2 ?? 0;
            }
            else if (isPlayer2)
            {
                goalsFor += match.GoalsPlayer2 ?? 0;
                goalsAgainst += match.GoalsPlayer1 ?? 0;
            }
        }

        return (goalsFor, goalsAgainst);
    }

    private static bool IsSamePlayer(string matchPlayer, SalesPortal.Domain.Cup.Competitor competitor)
    {
        return string.Equals(matchPlayer, competitor.PlayerId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(matchPlayer, competitor.Code, StringComparison.OrdinalIgnoreCase)
            || string.Equals(matchPlayer, competitor.Name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(matchPlayer, competitor.Team, StringComparison.OrdinalIgnoreCase);
    }
}
