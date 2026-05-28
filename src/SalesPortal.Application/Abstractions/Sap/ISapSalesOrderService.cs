using SalesPortal.Domain.Tenants;

namespace SalesPortal.Application.Abstractions.Sap
{
    public interface ISapSalesOrderService
    {
        Task<int?> CreateSalesOrderAsync(
            Tenant tenant,
            SapSalesOrderDraft order,
            CancellationToken cancellationToken);
    }
}
