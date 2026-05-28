using SalesPortal.Application.Abstractions.Authentication;
using SalesPortal.Application.Abstractions.Persistence;
using SalesPortal.Application.Abstractions.Security;
using SalesPortal.Application.Auth.Dtos;
using SalesPortal.Application.Common;
using SalesPortal.Domain.Cup;
using SalesPortal.Domain.Tenants;
using System;
using System.Collections.Generic;
using System.Text;

namespace SalesPortal.Application.Auth.Services
{
    public sealed class AuthService : IAuthService
    {
        private readonly ITenantResolver _tenantResolver;
        private readonly ICustomerRepository _customerRepository;
        private readonly ICupRepository _cupRepository;
        private readonly IPasswordHasher _passwordHasher;

        public AuthService(
            ITenantResolver tenantResolver,
            ICustomerRepository customerRepository,
            ICupRepository cupRepository,
            IPasswordHasher passwordHasher)
        {
            _tenantResolver = tenantResolver;
            _customerRepository = customerRepository;
            _cupRepository = cupRepository;
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

            CountryAssignmentInfo? countryAssignment = null;
            if (IsPlayer(customer.Role))
            {
                var assignmentResult = await EnsurePlayerCountryAssignmentAsync(
                    tenant,
                    customer.CardCode,
                    customer.CardName,
                    cancellationToken);

                if (assignmentResult.IsFailure)
                    return Result<LoginResult>.Failure(assignmentResult.Message);

                countryAssignment = assignmentResult.Data;
            }

            var result = new LoginResult
            {
                CardCode = customer.CardCode,
                CardName = customer.CardName,
                UserWeb = customer.UserWeb,
                EmailWeb = customer.EmailWeb,
                TenantCode = tenant.Code,
                MustChangePassword = customer.MustChangePassword,
                Role = customer.Role,
                CountryWasAssigned = countryAssignment?.WasAssigned ?? false,
                AssignedCountryCode = countryAssignment?.CountryCode ?? string.Empty,
                AssignedCountryName = countryAssignment?.CountryName ?? string.Empty
            };

            return Result<LoginResult>.Success(result);
        }


        public async Task<Result<RegisterResult>> RegisterAsync(
            RegisterRequest request,
            CancellationToken cancellationToken)
        {
            var identificationNumber = request.IdentificationNumber.Trim();
            var firstName = request.FirstName.Trim();
            var firstLastName = request.FirstLastName.Trim();
            var email = request.Email.Trim();

            if (string.IsNullOrWhiteSpace(identificationNumber))
                return Result<RegisterResult>.Failure("Ingrese el número de identificación.");

            if (string.IsNullOrWhiteSpace(firstName))
                return Result<RegisterResult>.Failure("Ingrese el primer nombre.");

            if (string.IsNullOrWhiteSpace(firstLastName))
                return Result<RegisterResult>.Failure("Ingrese el primer apellido.");

            if (string.IsNullOrWhiteSpace(email))
                return Result<RegisterResult>.Failure("Ingrese el correo electrónico.");

            var tenant = _tenantResolver.ResolveByHost(request.Host);
            var cardCode = $"P{identificationNumber}";
            var userWeb = BuildUserWeb(firstName, firstLastName);

            var existingBusinessPartner = await _customerRepository.ExistsByCardCodeOrIdentificationAsync(
                tenant,
                cardCode,
                identificationNumber,
                cancellationToken);

            if (existingBusinessPartner)
                return Result<RegisterResult>.Failure("Ya existe un socio de negocio registrado con esta identificación.");

            var existingUser = await _customerRepository.GetByUserOrEmailAsync(
                tenant,
                userWeb,
                cancellationToken);

            if (existingUser != null)
                return Result<RegisterResult>.Failure($"El usuario web generado ({userWeb}) ya existe. Contacte al administrador.");

            var existingEmail = await _customerRepository.GetByUserOrEmailAsync(
                tenant,
                email,
                cancellationToken);

            if (existingEmail != null)
                return Result<RegisterResult>.Failure("Ya existe un usuario registrado con este correo electrónico.");

            const string defaultPassword = "clave1234";

            try
            {
                await _customerRepository.CreatePortalCustomerAsync(
                    tenant,
                    new PortalCustomerRegistration
                    {
                        CardCode = cardCode,
                        CardName = $"{firstName} {firstLastName}",
                        IdentificationNumber = identificationNumber,
                        Email = email,
                        UserWeb = userWeb,
                        PasswordWeb = defaultPassword,
                        Dealer = "Y",
                        MustChangePassword = "Y",
                        Role = "P"
                    },
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Result<RegisterResult>.Failure(ex.Message);
            }

            return Result<RegisterResult>.Success(new RegisterResult
            {
                CardCode = cardCode,
                UserWeb = userWeb,
                DefaultPassword = defaultPassword
            }, "Registro creado correctamente.");
        }

        private async Task<Result<CountryAssignmentInfo>> EnsurePlayerCountryAssignmentAsync(
            Tenant tenant,
            string playerId,
            string playerName,
            CancellationToken cancellationToken)
        {
            var currentCompetitor = await _cupRepository.GetCompetitorByPlayerIdAsync(
                tenant,
                playerId,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(currentCompetitor?.Team))
                return Result<CountryAssignmentInfo>.Success(new CountryAssignmentInfo());

            var countries = await _cupRepository.GetWorldCupCountriesAsync(tenant, cancellationToken);
            if (countries.Count == 0)
                return Result<CountryAssignmentInfo>.Failure("No hay países disponibles para asignar al jugador.");

            var competitors = await _cupRepository.GetCompetitorsAsync(tenant, cancellationToken);
            var assignedTeams = competitors
                .Where(competitor => !string.Equals(competitor.PlayerId, playerId, StringComparison.OrdinalIgnoreCase))
                .Select(competitor => competitor.Team?.Trim())
                .Where(team => !string.IsNullOrWhiteSpace(team))
                .Select(team => team!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var availableCountries = countries
                .Where(country => GetCountryAssignmentKeys(country).Any())
                .Where(country => !CountryIsAssigned(country, assignedTeams))
                .ToList();

            if (availableCountries.Count == 0)
                return Result<CountryAssignmentInfo>.Failure("No hay países disponibles sin asignar para este jugador.");

            var selectedCountry = availableCountries[Random.Shared.Next(availableCountries.Count)];
            var selectedTeam = GetCountryTeamName(selectedCountry);

            await _cupRepository.SaveCompetitorAsync(tenant, new Competitor
            {
                Code = string.IsNullOrWhiteSpace(currentCompetitor?.Code) ? playerId : currentCompetitor.Code.Trim(),
                Name = string.IsNullOrWhiteSpace(currentCompetitor?.Name) ? playerName : currentCompetitor.Name.Trim(),
                PlayerId = playerId,
                Team = selectedTeam,
                Matches = currentCompetitor?.Matches ?? 0,
                Points = currentCompetitor?.Points ?? 0,
                GoalDifference = currentCompetitor?.GoalDifference ?? 0,
                TablePosition = currentCompetitor?.TablePosition ?? 0,
                Status = currentCompetitor?.Status ?? string.Empty
            }, cancellationToken);

            return Result<CountryAssignmentInfo>.Success(new CountryAssignmentInfo
            {
                WasAssigned = true,
                CountryCode = selectedCountry.CountryCode.Trim(),
                CountryName = selectedTeam
            });
        }


        private static string BuildUserWeb(string firstName, string firstLastName)
        {
            var firstInitial = firstName.Trim()[0].ToString();
            var lastName = firstLastName.Trim();

            return $"{firstInitial}{lastName}".ToUpperInvariant();
        }

        private static bool IsPlayer(string role)
        {
            return !string.Equals(role, "A", StringComparison.OrdinalIgnoreCase);
        }

        private static bool CountryIsAssigned(WorldCupCountry country, ISet<string> assignedTeams)
        {
            return GetCountryAssignmentKeys(country).Any(assignedTeams.Contains);
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

        private static string GetCountryTeamName(WorldCupCountry country)
        {
            return GetCountryAssignmentKeys(country).First();
        }

        private sealed class CountryAssignmentInfo
        {
            public bool WasAssigned { get; set; }
            public string CountryCode { get; set; } = string.Empty;
            public string CountryName { get; set; } = string.Empty;
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
