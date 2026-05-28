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
        return RedirectToAction(nameof(Players));
    }

    public async Task<IActionResult> Players(CancellationToken cancellationToken)
    {
        return View(await BuildPlayersModelAsync(cancellationToken));
    }

    public async Task<IActionResult> Matches(CancellationToken cancellationToken)
    {
        return View(await BuildMatchesModelAsync(cancellationToken));
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
}
