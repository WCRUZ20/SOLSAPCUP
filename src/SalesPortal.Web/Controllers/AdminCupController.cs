using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesPortal.Application.Abstractions.Persistence;
using SalesPortal.Domain.Cup;
using SalesPortal.Web.Models.Cup;

namespace SalesPortal.Web.Controllers;

[Authorize(Policy = "Admin")]
public sealed class AdminCupController : Controller
{
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
        var selectedCode = model.Competitor.Code.Trim();
        var existingCompetitor = (await _cupRepository.GetCompetitorsAsync(tenant, cancellationToken))
            .FirstOrDefault(c => string.Equals(c.Code, selectedCode, StringComparison.OrdinalIgnoreCase));

        if (existingCompetitor is null)
        {
            TempData["Error"] = "Seleccione un jugador existente desde el grid antes de editar.";
            return RedirectToAction(nameof(Players));
        }

        await _cupRepository.SaveCompetitorAsync(tenant, new Competitor
        {
            Code = existingCompetitor.Code,
            Name = existingCompetitor.Name,
            PlayerId = existingCompetitor.PlayerId,
            Team = existingCompetitor.Team,
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
        var selectedCode = model.Match.Code.Trim();
        var existingMatch = (await _cupRepository.GetMatchesAsync(tenant, cancellationToken))
            .FirstOrDefault(m => string.Equals(m.Code, selectedCode, StringComparison.OrdinalIgnoreCase));

        if (existingMatch is null)
        {
            TempData["Error"] = "Seleccione un partido existente desde el grid antes de editar.";
            return RedirectToAction(nameof(Matches));
        }

        await _cupRepository.SaveMatchAsync(tenant, new CupMatch
        {
            Code = existingMatch.Code,
            Name = existingMatch.Name,
            MatchDate = existingMatch.MatchDate,
            MatchTime = existingMatch.MatchTime,
            Player1 = existingMatch.Player1,
            Player2 = existingMatch.Player2,
            GoalsPlayer1 = model.Match.GoalsPlayer1,
            GoalsPlayer2 = model.Match.GoalsPlayer2,
            Observation = model.Match.Observation?.Trim() ?? string.Empty
        }, cancellationToken);

        TempData["Success"] = "Partido guardado correctamente.";
        return RedirectToAction(nameof(Matches));
    }

    private async Task<AdminCupPlayersViewModel> BuildPlayersModelAsync(CancellationToken cancellationToken)
    {
        var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);
        var competitors = await _cupRepository.GetCompetitorsAsync(tenant, cancellationToken);

        return new AdminCupPlayersViewModel
        {
            Competitors = competitors.Select(c => new CompetitorFormViewModel
            {
                Code = c.Code,
                Name = c.Name,
                PlayerId = c.PlayerId,
                Team = c.Team,
                Matches = c.Matches,
                Points = c.Points,
                GoalDifference = c.GoalDifference,
                TablePosition = c.TablePosition,
                Status = c.Status
            }).ToList()
        };
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
