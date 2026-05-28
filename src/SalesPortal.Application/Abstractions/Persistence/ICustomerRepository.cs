using SalesPortal.Application.Auth.Dtos;
using SalesPortal.Domain.Customers;
using SalesPortal.Domain.Tenants;
using System;
using System.Collections.Generic;
using System.Text;

namespace SalesPortal.Application.Abstractions.Persistence
{
    public interface ICustomerRepository
    {
        Task<CustomerPortalUser?> GetByUserOrEmailAsync(
            Tenant tenant,
            string userOrEmail,
            CancellationToken cancellationToken);

        Task<CustomerPortalUser?> GetByCardCodeAsync(
            Tenant tenant,
            string cardCode,
            CancellationToken cancellationToken);

        Task<bool> ExistsByCardCodeOrIdentificationAsync(
            Tenant tenant,
            string cardCode,
            string identificationNumber,
            CancellationToken cancellationToken);

        Task CreatePortalCustomerAsync(
            Tenant tenant,
            PortalCustomerRegistration registration,
            CancellationToken cancellationToken);

        Task UpdatePasswordAsync(
            Tenant tenant,
            string cardCode,
            string newPassword,
            CancellationToken cancellationToken);
    }
}
