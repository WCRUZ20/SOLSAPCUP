using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesPortal.Application.Abstractions.Persistence;
using SalesPortal.Shared.Security;
using SalesPortal.Web.Models.Cup;
using System.Security.Claims;

namespace SalesPortal.Web.Controllers;

[Authorize]
public sealed class MatchesController : Controller
{
    private readonly ITenantResolver _tenantResolver;
    private readonly ICupRepository _cupRepository;

    public MatchesController(ITenantResolver tenantResolver, ICupRepository cupRepository)
    {
        _tenantResolver = tenantResolver;
        _cupRepository = cupRepository;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);
        var playerId = User.FindFirstValue(PortalClaimTypes.CardCode) ?? string.Empty;
        var competitor = await _cupRepository.GetCompetitorByPlayerIdAsync(tenant, playerId, cancellationToken);
        var competitorCode = string.IsNullOrWhiteSpace(competitor?.Code) ? playerId : competitor.Code;
        var matches = User.IsInRole("Admin")
            ? await _cupRepository.GetMatchesAsync(tenant, cancellationToken)
            : await _cupRepository.GetMatchesForPlayerAsync(tenant, playerId, competitorCode, cancellationToken);

        return View(new MatchesViewModel
        {
            Matches = matches.Select(match => new MatchRowViewModel
            {
                Code = match.Code,
                Name = match.Name,
                MatchDate = match.MatchDate,
                MatchTime = match.MatchTime,
                Player1 = match.Player1,
                Player2 = match.Player2,
                GoalsPlayer1 = match.GoalsPlayer1,
                GoalsPlayer2 = match.GoalsPlayer2,
                Observation = match.Observation
            }).ToList()
        });
    }
}
