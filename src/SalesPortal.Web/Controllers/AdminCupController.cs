using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesPortal.Application.Abstractions.Persistence;
using SalesPortal.Domain.Cup;
using SalesPortal.Web.Models.Cup;

namespace SalesPortal.Web.Controllers;

[Authorize(Policy = "Admin")]
public sealed class AdminCupController : Controller
{
    private static readonly DateTime MatchScheduleStartDate = new(2026, 6, 12);
    private static readonly TimeSpan MatchScheduleStartTime = TimeSpan.FromHours(17);
    private static readonly TimeSpan MatchScheduleInterval = TimeSpan.FromMinutes(10);

    private readonly ITenantResolver _tenantResolver;
    private readonly ICupRepository _cupRepository;

    public AdminCupController(ITenantResolver tenantResolver, ICupRepository cupRepository)
    {
        _tenantResolver = tenantResolver;
        _cupRepository = cupRepository;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> Players(CancellationToken cancellationToken)
    {
        return View(await BuildPlayersModelAsync(cancellationToken));
    }

    public async Task<IActionResult> Matches(CancellationToken cancellationToken)
    {
        return View(await BuildMatchesModelAsync(cancellationToken));
    }

    public async Task<IActionResult> Countries(CancellationToken cancellationToken)
    {
        return View(await BuildCountriesModelAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCompetitor(AdminCupPlayersViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Competitor.Code) || string.IsNullOrWhiteSpace(model.Competitor.Name))
        {
            TempData["Error"] = "Ingrese código y nombre del jugador.";
            return RedirectToAction(nameof(Players));
        }

        var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);
        await _cupRepository.SaveCompetitorAsync(tenant, new Competitor
        {
            Code = model.Competitor.Code.Trim(),
            Name = model.Competitor.Name.Trim(),
            PlayerId = model.Competitor.PlayerId?.Trim() ?? string.Empty,
            Team = model.Competitor.Team?.Trim() ?? string.Empty,
            Matches = model.Competitor.Matches,
            Points = model.Competitor.Points,
            GoalDifference = model.Competitor.GoalDifference,
            TablePosition = model.Competitor.TablePosition,
            Status = model.Competitor.Status?.Trim() ?? string.Empty
        }, cancellationToken);

        TempData["Success"] = "Jugador guardado correctamente.";
        return RedirectToAction(nameof(Players));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCountry(AdminCupCountriesViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Country.CountryCode) || string.IsNullOrWhiteSpace(model.Country.CountryName))
        {
            TempData["Error"] = "Ingrese código y nombre del país.";
            return RedirectToAction(nameof(Countries));
        }

        var countryName = model.Country.CountryName.Trim();
        var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);
        await _cupRepository.SaveWorldCupCountryAsync(tenant, new WorldCupCountry
        {
            Code = model.Country.Code,
            Name = string.IsNullOrWhiteSpace(model.Country.Name) ? countryName : model.Country.Name.Trim(),
            CountryCode = model.Country.CountryCode.Trim(),
            CountryName = countryName
        }, cancellationToken);

        TempData["Success"] = "País guardado correctamente.";
        return RedirectToAction(nameof(Countries));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMatch(AdminCupMatchesViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Match.Code) || string.IsNullOrWhiteSpace(model.Match.Name))
        {
            TempData["Error"] = "Ingrese código y nombre del partido.";
            return RedirectToAction(nameof(Matches));
        }

        var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);
        await _cupRepository.SaveMatchAsync(tenant, new CupMatch
        {
            Code = model.Match.Code.Trim(),
            Name = model.Match.Name.Trim(),
            MatchDate = model.Match.MatchDate,
            MatchTime = model.Match.MatchTime,
            Player1 = model.Match.Player1?.Trim() ?? string.Empty,
            Player2 = model.Match.Player2?.Trim() ?? string.Empty,
            GoalsPlayer1 = model.Match.GoalsPlayer1,
            GoalsPlayer2 = model.Match.GoalsPlayer2,
            Observation = model.Match.Observation?.Trim() ?? string.Empty
        }, cancellationToken);
        await RefreshStandingsAsync(tenant, cancellationToken);

        TempData["Success"] = "Partido guardado correctamente. Tabla de posiciones actualizada.";
        return RedirectToAction(nameof(Matches));
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CalculateMatches(CancellationToken cancellationToken)
    {
        var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);
        var competitors = await _cupRepository.GetCompetitorsAsync(tenant, cancellationToken);
        var activeCompetitors = competitors
            .Where(IsActiveCompetitor)
            .OrderBy(competitor => competitor.TablePosition <= 0 ? decimal.MaxValue : competitor.TablePosition)
            .ThenBy(competitor => competitor.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(competitor => competitor.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (activeCompetitors.Count < 2)
        {
            TempData["Error"] = "Se necesitan al menos dos jugadores activos para calcular partidos.";
            return RedirectToAction(nameof(Matches));
        }

        if (activeCompetitors.Count % 2 != 0)
        {
            TempData["Error"] = "La cantidad de jugadores activos debe ser par para calcular partidos.";
            return RedirectToAction(nameof(Matches));
        }

        var existingMatches = await _cupRepository.GetMatchesAsync(tenant, cancellationToken);
        var schedule = BuildRoundRobinSchedule(activeCompetitors, MatchScheduleStartDate);
        var usedCodes = existingMatches
            .Select(match => match.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var createdCount = 0;
        var updatedCount = 0;

        foreach (var scheduledMatch in schedule)
        {
            var existingMatch = FindExistingMatchForPair(existingMatches, scheduledMatch.Player1, scheduledMatch.Player2, activeCompetitors);

            if (existingMatch == null)
            {
                var code = BuildNextMatchCode(usedCodes);
                usedCodes.Add(code);

                await _cupRepository.SaveMatchAsync(tenant, new CupMatch
                {
                    Code = code,
                    Name = scheduledMatch.Name,
                    MatchDate = scheduledMatch.MatchDate,
                    MatchTime = scheduledMatch.MatchTime,
                    Player1 = scheduledMatch.Player1.Code,
                    Player2 = scheduledMatch.Player2.Code,
                    Observation = scheduledMatch.Observation
                }, cancellationToken);
                createdCount++;
                continue;
            }

            if (!IsPendingMatch(existingMatch))
                continue;

            var shouldUpdateMatch = false;

            if (!existingMatch.MatchDate.HasValue)
            {
                existingMatch.MatchDate = scheduledMatch.MatchDate;
                shouldUpdateMatch = true;
            }

            if (!existingMatch.MatchTime.HasValue)
            {
                existingMatch.MatchTime = scheduledMatch.MatchTime;
                shouldUpdateMatch = true;
            }

            if (string.IsNullOrWhiteSpace(existingMatch.Name))
            {
                existingMatch.Name = scheduledMatch.Name;
                shouldUpdateMatch = true;
            }

            if (string.IsNullOrWhiteSpace(existingMatch.Observation))
            {
                existingMatch.Observation = scheduledMatch.Observation;
                shouldUpdateMatch = true;
            }

            if (!shouldUpdateMatch)
                continue;

            await _cupRepository.SaveMatchAsync(tenant, existingMatch, cancellationToken);
            updatedCount++;
        }

        await RefreshStandingsAsync(tenant, cancellationToken);

        TempData["Success"] = createdCount == 0 && updatedCount == 0
            ? $"El calendario ya estaba completo para {activeCompetitors.Count} jugadores activos."
            : $"Calendario calculado: {createdCount} partidos creados y {updatedCount} partidos actualizados para {activeCompetitors.Count} jugadores activos.";
        return RedirectToAction(nameof(Matches));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCompetitor(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            TempData["Error"] = "Seleccione un jugador para eliminar.";
            return RedirectToAction(nameof(Players));
        }

        var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);
        await _cupRepository.DeleteCompetitorAsync(tenant, code.Trim(), cancellationToken);

        TempData["Success"] = "Jugador eliminado correctamente.";
        return RedirectToAction(nameof(Players));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMatch(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            TempData["Error"] = "Seleccione un partido para eliminar.";
            return RedirectToAction(nameof(Matches));
        }

        var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);
        await _cupRepository.DeleteMatchAsync(tenant, code.Trim(), cancellationToken);
        await RefreshStandingsAsync(tenant, cancellationToken);

        TempData["Success"] = "Partido eliminado correctamente. Tabla de posiciones actualizada.";
        return RedirectToAction(nameof(Matches));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCountry(int? code, CancellationToken cancellationToken)
    {
        if (!code.HasValue)
        {
            TempData["Error"] = "Seleccione un país para eliminar.";
            return RedirectToAction(nameof(Countries));
        }

        var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);
        await _cupRepository.DeleteWorldCupCountryAsync(tenant, code.Value, cancellationToken);

        TempData["Success"] = "País eliminado correctamente.";
        return RedirectToAction(nameof(Countries));
    }


    private static IReadOnlyList<ScheduledMatch> BuildRoundRobinSchedule(IReadOnlyList<Competitor> competitors, DateTime startDate)
    {
        var rotation = competitors.ToList();
        var rounds = competitors.Count - 1;
        var matchesPerRound = competitors.Count / 2;
        var schedule = new List<ScheduledMatch>(rounds * matchesPerRound);

        for (var roundIndex = 0; roundIndex < rounds; roundIndex++)
        {
            var matchDate = startDate.Date.AddDays(roundIndex);

            for (var matchIndex = 0; matchIndex < matchesPerRound; matchIndex++)
            {
                var player1 = rotation[matchIndex];
                var player2 = rotation[competitors.Count - 1 - matchIndex];

                if (roundIndex % 2 == 1)
                    (player1, player2) = (player2, player1);

                schedule.Add(new ScheduledMatch(
                    player1,
                    player2,
                    matchDate,
                    BuildMatchTime(matchIndex),
                    $"{GetCompetitorDisplayName(player1)} vs {GetCompetitorDisplayName(player2)}",
                    $"Generado automáticamente - Jornada {roundIndex + 1}"));
            }

            var last = rotation[^1];
            rotation.RemoveAt(rotation.Count - 1);
            rotation.Insert(1, last);
        }

        return schedule;
    }

    private static short BuildMatchTime(int matchIndex)
    {
        var totalMinutes = (int)MatchScheduleStartTime.TotalMinutes + ((int)MatchScheduleInterval.TotalMinutes * matchIndex);
        var hour = (totalMinutes / 60) % 24;
        var minute = totalMinutes % 60;

        return (short)((hour * 100) + minute);
    }

    private static CupMatch? FindExistingMatchForPair(
        IEnumerable<CupMatch> matches,
        Competitor player1,
        Competitor player2,
        IEnumerable<Competitor> activeCompetitors)
    {
        var expectedPairKey = BuildPairKey(player1.Code, player2.Code);

        return matches.FirstOrDefault(match =>
        {
            var matchPlayer1 = FindCompetitorForMatchPlayer(match.Player1, activeCompetitors);
            var matchPlayer2 = FindCompetitorForMatchPlayer(match.Player2, activeCompetitors);

            if (matchPlayer1 == null || matchPlayer2 == null || ReferenceEquals(matchPlayer1, matchPlayer2))
                return false;

            return string.Equals(expectedPairKey, BuildPairKey(matchPlayer1.Code, matchPlayer2.Code), StringComparison.OrdinalIgnoreCase);
        });
    }

    private static Competitor? FindCompetitorForMatchPlayer(string matchPlayer, IEnumerable<Competitor> competitors)
    {
        if (string.IsNullOrWhiteSpace(matchPlayer))
            return null;

        return competitors.FirstOrDefault(competitor => IsSamePlayer(matchPlayer, competitor));
    }

    private static bool IsActiveCompetitor(Competitor competitor)
    {
        return string.IsNullOrWhiteSpace(competitor.Status)
            || string.Equals(competitor.Status.Trim(), "Activo", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPendingMatch(CupMatch match)
    {
        return !match.GoalsPlayer1.HasValue && !match.GoalsPlayer2.HasValue;
    }

    private static string BuildNextMatchCode(ISet<string> usedCodes)
    {
        var nextNumber = usedCodes
            .Select(TryGetAutomaticMatchNumber)
            .DefaultIfEmpty(0)
            .Max() + 1;

        string code;
        do
        {
            code = $"AUTO{nextNumber:0000}";
            nextNumber++;
        }
        while (usedCodes.Contains(code));

        return code;
    }

    private static int TryGetAutomaticMatchNumber(string code)
    {
        return code.StartsWith("AUTO", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(code[4..], out var number)
                ? number
                : 0;
    }

    private static string BuildPairKey(string player1Code, string player2Code)
    {
        var players = new[] { player1Code.Trim(), player2Code.Trim() }
            .OrderBy(player => player, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return string.Join("|", players);
    }

    private static string GetCompetitorDisplayName(Competitor competitor)
    {
        if (!string.IsNullOrWhiteSpace(competitor.Team))
            return competitor.Team.Trim();

        if (!string.IsNullOrWhiteSpace(competitor.Name))
            return competitor.Name.Trim();

        return competitor.Code.Trim();
    }

    private async Task RefreshStandingsAsync(SalesPortal.Domain.Tenants.Tenant tenant, CancellationToken cancellationToken)
    {
        var competitors = await _cupRepository.GetCompetitorsAsync(tenant, cancellationToken);
        var matches = await _cupRepository.GetMatchesAsync(tenant, cancellationToken);
        var today = DateTime.Today;
        var statistics = competitors
            .Select(competitor => new CompetitorStatistics(competitor))
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
            row.Competitor.Matches = row.Matches;
            row.Competitor.Points = row.Points;
            row.Competitor.GoalDifference = row.GoalDifference;
            row.Competitor.TablePosition = position++;

            await _cupRepository.SaveCompetitorAsync(tenant, row.Competitor, cancellationToken);
        }
    }

    private static bool IsPlayedMatch(CupMatch match, DateTime today)
    {
        return match.MatchDate.HasValue
            && match.MatchDate.Value.Date <= today
            && match.GoalsPlayer1.HasValue
            && match.GoalsPlayer2.HasValue;
    }

    private static CompetitorStatistics? FindStatisticsForPlayer(string matchPlayer, IEnumerable<CompetitorStatistics> statistics)
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


    private sealed record ScheduledMatch(
        Competitor Player1,
        Competitor Player2,
        DateTime MatchDate,
        short? MatchTime,
        string Name,
        string Observation);

    private sealed class CompetitorStatistics
    {
        public CompetitorStatistics(Competitor competitor)
        {
            Competitor = competitor;
        }

        public Competitor Competitor { get; }
        public decimal Matches { get; private set; }
        public decimal Points { get; private set; }
        public decimal GoalDifference { get; private set; }

        public void ApplyMatch(decimal goalsFor, decimal goalsAgainst)
        {
            Matches++;
            GoalDifference += goalsFor - goalsAgainst;

            if (goalsFor > goalsAgainst)
                Points += 3;
            else if (goalsFor == goalsAgainst)
                Points += 1;
        }
    }

    private async Task<AdminCupPlayersViewModel> BuildPlayersModelAsync(CancellationToken cancellationToken)
    {
        var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);
        var competitors = await _cupRepository.GetCompetitorsAsync(tenant, cancellationToken);
        var countries = await _cupRepository.GetWorldCupCountriesAsync(tenant, cancellationToken);

        return new AdminCupPlayersViewModel
        {
            Competitors = competitors.Select(c =>
            {
                var country = FindCountryForTeam(countries, c.Team);
                return new CompetitorFormViewModel
                {
                    Code = c.Code,
                    Name = c.Name,
                    PlayerId = c.PlayerId,
                    Team = c.Team,
                    CountryCode = country?.CountryCode.Trim() ?? string.Empty,
                    CountryName = country == null ? c.Team : GetCountryDisplayName(country),
                    Matches = c.Matches,
                    Points = c.Points,
                    GoalDifference = c.GoalDifference,
                    TablePosition = c.TablePosition,
                    Status = c.Status
                };
            }).ToList()
        };
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

    private async Task<AdminCupMatchesViewModel> BuildMatchesModelAsync(CancellationToken cancellationToken)
    {
        var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);
        var matches = await _cupRepository.GetMatchesAsync(tenant, cancellationToken);

        return new AdminCupMatchesViewModel
        {
            Matches = matches.Select(m => new MatchFormViewModel
            {
                Code = m.Code,
                Name = m.Name,
                MatchDate = m.MatchDate,
                MatchTime = m.MatchTime,
                Player1 = m.Player1,
                Player2 = m.Player2,
                GoalsPlayer1 = m.GoalsPlayer1,
                GoalsPlayer2 = m.GoalsPlayer2,
                Observation = m.Observation
            }).ToList()
        };
    }

    private async Task<AdminCupCountriesViewModel> BuildCountriesModelAsync(CancellationToken cancellationToken)
    {
        var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);
        var countries = await _cupRepository.GetWorldCupCountriesAsync(tenant, cancellationToken);

        return new AdminCupCountriesViewModel
        {
            Countries = countries.Select(c => new CountryFormViewModel
            {
                Code = c.Code,
                Name = c.Name,
                CountryCode = c.CountryCode,
                CountryName = c.CountryName
            }).ToList()
        };
    }
}
