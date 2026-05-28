using SalesPortal.Domain.Cup;
using SalesPortal.Domain.Tenants;

namespace SalesPortal.Application.Abstractions.Persistence
{
    public interface ICupRepository
    {
        Task<IReadOnlyList<Competitor>> GetCompetitorsAsync(Tenant tenant, CancellationToken cancellationToken);
        Task<Competitor?> GetCompetitorByPlayerIdAsync(Tenant tenant, string playerId, CancellationToken cancellationToken);
        Task<IReadOnlyList<CupMatch>> GetMatchesAsync(Tenant tenant, CancellationToken cancellationToken);
        Task<IReadOnlyList<WorldCupCountry>> GetWorldCupCountriesAsync(Tenant tenant, CancellationToken cancellationToken);
        Task<IReadOnlyList<CupMatch>> GetMatchesForPlayerAsync(Tenant tenant, string playerId, string competitorCode, CancellationToken cancellationToken);
        Task SaveCompetitorAsync(Tenant tenant, Competitor competitor, CancellationToken cancellationToken);
        Task SaveMatchAsync(Tenant tenant, CupMatch match, CancellationToken cancellationToken);
        Task SaveWorldCupCountryAsync(Tenant tenant, WorldCupCountry country, CancellationToken cancellationToken);
    }
}
