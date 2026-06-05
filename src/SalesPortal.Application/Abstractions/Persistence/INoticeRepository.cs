using SalesPortal.Domain.Notices;
using SalesPortal.Domain.Tenants;

namespace SalesPortal.Application.Abstractions.Persistence
{
    public interface INoticeRepository
    {
        Task<IReadOnlyList<LoginNotice>> GetNoticesAsync(Tenant tenant, CancellationToken cancellationToken);
        Task<IReadOnlyList<LoginNotice>> GetActiveLoginNoticesAsync(Tenant tenant, DateTime currentDate, CancellationToken cancellationToken);
        Task SaveNoticeAsync(Tenant tenant, LoginNotice notice, CancellationToken cancellationToken);
        Task DeleteNoticeAsync(Tenant tenant, string code, CancellationToken cancellationToken);
    }
}
