using SalesPortal.Application.Abstractions.Authentication;
using SalesPortal.Application.Abstractions.Persistence;
using SalesPortal.Application.Abstractions.Security;
using SalesPortal.Application.Auth.Dtos;
using SalesPortal.Application.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace SalesPortal.Application.Auth.Services
{
    public sealed class AuthService : IAuthService
    {
        private readonly ITenantResolver _tenantResolver;
        private readonly ICustomerRepository _customerRepository;
        private readonly IPasswordHasher _passwordHasher;

        public AuthService(
            ITenantResolver tenantResolver,
            ICustomerRepository customerRepository,
            IPasswordHasher passwordHasher)
        {
            _tenantResolver = tenantResolver;
            _customerRepository = customerRepository;
            _passwordHasher = passwordHasher;
        }

        public async Task<Result<LoginResult>> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.UserOrEmail))
                return Result<LoginResult>.Failure("Ingrese su usuario o correo electrónico.");

            if (string.IsNullOrWhiteSpace(request.Password))
                return Result<LoginResult>.Failure("Ingrese su contraseña.");

            var tenant = _tenantResolver.ResolveByHost(request.Host);

            var customer = await _customerRepository.GetByUserOrEmailAsync(
                tenant,
                request.UserOrEmail.Trim(),
                cancellationToken);

            if (customer == null)
                return Result<LoginResult>.Failure("Usuario o contraseña incorrectos.");

            if (!customer.IsActive)
                return Result<LoginResult>.Failure("El cliente no se encuentra activo.");

            var passwordIsValid = _passwordHasher.Verify(
                request.Password,
                customer.PasswordWeb);

            if (!passwordIsValid)
                return Result<LoginResult>.Failure("Usuario o contraseña incorrectos.");

            var result = new LoginResult
            {
                CardCode = customer.CardCode,
                CardName = customer.CardName,
                UserWeb = customer.UserWeb,
                EmailWeb = customer.EmailWeb,
                TenantCode = tenant.Code,
                MustChangePassword = customer.MustChangePassword,
                Role = customer.Role
            };

            return Result<LoginResult>.Success(result);
        }

        public async Task<Result> ChangePasswordAsync(
            ChangePasswordRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.CardCode))
                return Result.Failure("No se pudo identificar el cliente.");

            if (string.IsNullOrWhiteSpace(request.NewPassword))
                return Result.Failure("Ingrese la nueva contraseña.");

            if (request.NewPassword.Length < 8)
                return Result.Failure("La contraseña debe tener mínimo 8 caracteres.");

            if (request.NewPassword != request.ConfirmPassword)
                return Result.Failure("Las contraseñas no coinciden.");

            var tenant = _tenantResolver.ResolveByHost(request.Host);

            var customer = await _customerRepository.GetByCardCodeAsync(
                tenant,
                request.CardCode,
                cancellationToken);

            if (customer == null)
                return Result.Failure("Cliente no encontrado.");

            var newPasswordHash = _passwordHasher.Hash(request.NewPassword);

            await _customerRepository.UpdatePasswordAsync(
                tenant,
                request.CardCode,
                newPasswordHash,
                cancellationToken);

            return Result.Success("Contraseña actualizada correctamente.");
        }
    }
}
