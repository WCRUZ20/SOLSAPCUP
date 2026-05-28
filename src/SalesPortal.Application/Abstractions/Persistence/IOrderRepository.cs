using SalesPortal.Domain.Orders;
using SalesPortal.Domain.Tenants;
using System;
using System.Collections.Generic;

namespace SalesPortal.Application.Abstractions.Persistence
{
    public interface IOrderRepository
    {
        Task<IReadOnlyList<CustomerOrder>> GetCustomerOrdersAsync(
            Tenant tenant,
            string cardCode,
            DateTime? dateFrom,
            DateTime? dateTo,
            int? docNum,
            CancellationToken cancellationToken);

        Task<CustomerOrderDetail?> GetCustomerOrderDetailAsync(
            Tenant tenant,
            string cardCode,
            int docEntry,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<SalesItem>> GetSalesItemsAsync(
            Tenant tenant,
            string cardCode,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<ShippingAddress>> GetShippingAddressesAsync(
            Tenant tenant,
            string cardCode,
            CancellationToken cancellationToken);
    }
}
