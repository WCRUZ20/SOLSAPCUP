using SalesPortal.Application.Abstractions.Persistence;
using SalesPortal.Application.Auth.Dtos;
using SalesPortal.Domain.Customers;
using SalesPortal.Domain.Tenants;
using SalesPortal.Shared.Constants;
using System;
using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SalesPortal.Infrastructure.Persistence.Sap
{
    public sealed class SapCustomerRepository : ICustomerRepository
    {
        private readonly SapConnectionFactory _connectionFactory;

        public SapCustomerRepository(SapConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<CustomerPortalUser?> GetByUserOrEmailAsync(
            Tenant tenant,
            string userOrEmail,
            CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = $@"
                SELECT {dialect.TopOneClause}
                    {dialect.Identifier("CardCode")},
                    {dialect.Identifier("CardName")},
                    {dialect.Identifier(SapUserFields.UserWeb)} AS {dialect.Identifier("UserWeb")},
                    {dialect.Identifier(SapUserFields.EmailWeb)} AS {dialect.Identifier("EmailWeb")},
                    {dialect.Identifier(SapUserFields.PwdWeb)} AS {dialect.Identifier("PasswordWeb")},
                    {dialect.Identifier(SapUserFields.ChangePwd)} AS {dialect.Identifier("ChangePwd")},
                    {dialect.Identifier(SapUserFields.Role)} AS {dialect.Identifier("Role")},
                    {dialect.Identifier("validFor")}
                FROM {dialect.Table("OCRD")}
                WHERE 
                    {dialect.Identifier("CardType")} = 'C'
                    AND {dialect.Identifier("validFor")} = 'Y'
                    AND (
                        LOWER({dialect.NullValueFunction}({dialect.Identifier(SapUserFields.UserWeb)}, '')) = LOWER({dialect.Parameter(0)})
                        OR LOWER({dialect.NullValueFunction}({dialect.Identifier(SapUserFields.EmailWeb)}, '')) = LOWER({dialect.Parameter(1)})
                    ){dialect.LimitOneClause}";

            await using var connection = _connectionFactory.CreateConnection(tenant);
            await connection.OpenAsync(cancellationToken);

            await using var command = CreateCommand(connection, sql);
            AddParameter(command, dialect.Parameter(0), userOrEmail);
            AddParameter(command, dialect.Parameter(1), userOrEmail);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return MapCustomer(reader);
        }

        public async Task<CustomerPortalUser?> GetByCardCodeAsync(
            Tenant tenant,
            string cardCode,
            CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = $@"
                SELECT {dialect.TopOneClause}
                    {dialect.Identifier("CardCode")},
                    {dialect.Identifier("CardName")},
                    {dialect.Identifier(SapUserFields.UserWeb)} AS {dialect.Identifier("UserWeb")},
                    {dialect.Identifier(SapUserFields.EmailWeb)} AS {dialect.Identifier("EmailWeb")},
                    {dialect.Identifier(SapUserFields.PwdWeb)} AS {dialect.Identifier("PasswordWeb")},
                    {dialect.Identifier(SapUserFields.ChangePwd)} AS {dialect.Identifier("ChangePwd")},
                    {dialect.Identifier(SapUserFields.Role)} AS {dialect.Identifier("Role")},
                    {dialect.Identifier("validFor")}
                FROM {dialect.Table("OCRD")}
                WHERE 
                    {dialect.Identifier("CardType")} = 'C'
                    AND {dialect.Identifier("CardCode")} = {dialect.Parameter(0)}{dialect.LimitOneClause}";

            await using var connection = _connectionFactory.CreateConnection(tenant);
            await connection.OpenAsync(cancellationToken);

            await using var command = CreateCommand(connection, sql);
            AddParameter(command, dialect.Parameter(0), cardCode);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return MapCustomer(reader);
        }


        public async Task<bool> ExistsByCardCodeOrIdentificationAsync(
            Tenant tenant,
            string cardCode,
            string identificationNumber,
            CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = $@"
                SELECT {dialect.TopOneClause}
                    {dialect.Identifier("CardCode")}
                FROM {dialect.Table("OCRD")}
                WHERE
                    {dialect.Identifier("CardCode")} = {dialect.Parameter(0)}
                    OR {dialect.Identifier("LicTradNum")} = {dialect.Parameter(1)}{dialect.LimitOneClause}";

            await using var connection = _connectionFactory.CreateConnection(tenant);
            await connection.OpenAsync(cancellationToken);

            await using var command = CreateCommand(connection, sql);
            AddParameter(command, dialect.Parameter(0), cardCode);
            AddParameter(command, dialect.Parameter(1), identificationNumber);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result != null && result != DBNull.Value;
        }

        public async Task CreatePortalCustomerAsync(
    Tenant tenant,
    PortalCustomerRegistration registration,
    CancellationToken cancellationToken)
        {
            var serviceLayer = tenant.ServiceLayer;

            if (serviceLayer == null || string.IsNullOrWhiteSpace(serviceLayer.BaseUrl))
                throw new InvalidOperationException("El Service Layer no está configurado para crear socios de negocio.");

            using var handler = new HttpClientHandler
            {
                CookieContainer = new CookieContainer()
            };

            if (serviceLayer.AllowInvalidCertificate)
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            using var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

            string? sessionCookie = null;

            try
            {
                // 1. Login al Service Layer.
                // Si falla, aquí se detiene el flujo y no intenta crear el SN.
                sessionCookie = await LoginAsync(
                    httpClient,
                    tenant,
                    serviceLayer,
                    cancellationToken);

                // 2. Construcción del payload para SAP Business One.
                var businessPartner = new Dictionary<string, object?>
                {
                    ["CardCode"] = registration.CardCode,
                    ["CardName"] = registration.CardName,
                    ["CardType"] = "cCustomer",
                    ["FederalTaxID"] = registration.IdentificationNumber,
                    ["EmailAddress"] = registration.Email,

                    // Campos de usuario SAP.
                    [SapUserFields.UserWeb] = registration.UserWeb,
                    [SapUserFields.EmailWeb] = registration.Email,
                    [SapUserFields.PwdWeb] = registration.PasswordWeb,
                    [SapUserFields.ChangePwd] = "Y",
                    //[SapUserFields.Role] = "P"
                };

                if (!string.IsNullOrWhiteSpace(registration.Dealer))
                    businessPartner[SapUserFields.Dealer] = registration.Dealer;

                //if (serviceLayer.DefaultSeries.HasValue)
                //    businessPartner["Series"] = serviceLayer.DefaultSeries.Value;

                // 3. Crear Socio de Negocio usando la cookie obtenida del Login.
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    BuildEndpoint(serviceLayer, "BusinessPartners"));

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("Cookie", sessionCookie);
                request.Content = CreateJsonContent(businessPartner);

                using var createResponse = await httpClient.SendAsync(request, cancellationToken);

                await EnsureSuccessAsync(
                    createResponse,
                    "No se pudo crear el socio de negocio en SAP.",
                    cancellationToken);
            }
            finally
            {
                // 4. Cerrar sesión si se logró iniciar sesión.
                if (!string.IsNullOrWhiteSpace(sessionCookie))
                {
                    await LogoutAsync(httpClient, serviceLayer, sessionCookie, cancellationToken);
                }
            }
        }

        private async Task<string> LoginAsync(
    HttpClient httpClient,
    Tenant tenant,
    TenantServiceLayerSettings serviceLayer,
    CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(serviceLayer.UserName))
                throw new InvalidOperationException("El usuario del Service Layer no está configurado.");

            if (string.IsNullOrWhiteSpace(serviceLayer.Password))
                throw new InvalidOperationException("La contraseña del Service Layer no está configurada.");

            var payload = new
            {
                CompanyDB = string.IsNullOrWhiteSpace(serviceLayer.CompanyDB)
                    ? tenant.Database
                    : serviceLayer.CompanyDB,
                UserName = serviceLayer.UserName,
                Password = serviceLayer.Password
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                BuildEndpoint(serviceLayer, "Login"));

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = CreateJsonContent(payload);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var sapMessage = ExtractSapErrorMessage(responseBody);

                var message = string.IsNullOrWhiteSpace(sapMessage)
                    ? "No se pudo iniciar sesión en SAP Service Layer."
                    : $"No se pudo iniciar sesión en SAP Service Layer. {sapMessage}";

                throw new InvalidOperationException(message);
            }

            if (!response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
                throw new InvalidOperationException("SAP Service Layer no devolvió cookies de sesión.");

            var sessionCookie = string.Join("; ",
                setCookieHeaders
                    .Select(header => header.Split(';')[0])
                    .Where(cookie => !string.IsNullOrWhiteSpace(cookie)));

            if (string.IsNullOrWhiteSpace(sessionCookie))
                throw new InvalidOperationException("SAP Service Layer devolvió cookies vacías.");

            return sessionCookie;
        }

        private static Uri BuildEndpoint(TenantServiceLayerSettings serviceLayer, string resource)
        {
            var baseUrl = serviceLayer.BaseUrl.TrimEnd('/');
            return new Uri($"{baseUrl}/{resource.TrimStart('/')}", UriKind.Absolute);
        }

        private async Task LogoutAsync(
    HttpClient httpClient,
    TenantServiceLayerSettings serviceLayer,
    string sessionCookie,
    CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    BuildEndpoint(serviceLayer, "Logout"));

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("Cookie", sessionCookie);

                using var response = await httpClient.SendAsync(request, cancellationToken);

                // No lanzamos excepción aquí para no ocultar errores reales del Create.
            }
            catch
            {
                // Intencionalmente silencioso.
                // El logout no debe romper el flujo principal.
            }
        }

        private static StringContent CreateJsonContent<TPayload>(TPayload payload)
        {
            var json = JsonSerializer.Serialize(payload);
            return new StringContent(json, encoding: null, mediaType: "application/json");
        }

        public async Task UpdatePasswordAsync(
            Tenant tenant,
            string cardCode,
            string newPassword,
            CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = $@"
                UPDATE {dialect.Table("OCRD")}
                SET 
                    {dialect.Identifier(SapUserFields.PwdWeb)} = {dialect.Parameter(0)},
                    {dialect.Identifier(SapUserFields.ChangePwd)} = 'N'
                WHERE {dialect.Identifier("CardCode")} = {dialect.Parameter(1)}";

            await using var connection = _connectionFactory.CreateConnection(tenant);
            await connection.OpenAsync(cancellationToken);

            await using var command = CreateCommand(connection, sql);
            AddParameter(command, dialect.Parameter(0), newPassword);
            AddParameter(command, dialect.Parameter(1), cardCode);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }


        private static async Task EnsureSuccessAsync(
            HttpResponseMessage response,
            string defaultMessage,
            CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
                return;

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            var sapMessage = ExtractSapErrorMessage(responseText);
            var message = string.IsNullOrWhiteSpace(sapMessage)
                ? defaultMessage
                : $"{defaultMessage} {sapMessage}";

            throw new InvalidOperationException(message);
        }

        private static string ExtractSapErrorMessage(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
                return string.Empty;

            try
            {
                using var document = JsonDocument.Parse(responseText);

                if (document.RootElement.TryGetProperty("error", out var error) &&
                    error.TryGetProperty("message", out var message))
                {
                    if (message.ValueKind == JsonValueKind.Object &&
                        message.TryGetProperty("value", out var value))
                        return value.GetString() ?? string.Empty;

                    if (message.ValueKind == JsonValueKind.String)
                        return message.GetString() ?? string.Empty;
                }
            }
            catch (JsonException)
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private static DbCommand CreateCommand(DbConnection connection, string sql)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            return command;
        }

        private static void AddParameter(DbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        private static CustomerPortalUser MapCustomer(IDataRecord reader)
        {
            return new CustomerPortalUser
            {
                CardCode = reader["CardCode"]?.ToString() ?? string.Empty,
                CardName = reader["CardName"]?.ToString() ?? string.Empty,
                UserWeb = reader["UserWeb"]?.ToString() ?? string.Empty,
                EmailWeb = reader["EmailWeb"]?.ToString() ?? string.Empty,
                PasswordWeb = reader["PasswordWeb"]?.ToString() ?? string.Empty,
                MustChangePassword = (reader["ChangePwd"]?.ToString() ?? "N") == "Y",
                Role = NormalizeRole(reader["Role"]?.ToString()),
                IsActive = (reader["validFor"]?.ToString() ?? "N") == "Y"
            };
        }
        private static string NormalizeRole(string? role)
        {
            return string.Equals(role, "A", StringComparison.OrdinalIgnoreCase) ? "A" : "P";
        }
    }
}
