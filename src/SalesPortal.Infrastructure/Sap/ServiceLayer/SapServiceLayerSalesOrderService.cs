using Microsoft.Extensions.Logging;
using SalesPortal.Application.Abstractions.Sap;
using SalesPortal.Domain.Tenants;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SalesPortal.Infrastructure.Sap.ServiceLayer
{
    public sealed class SapServiceLayerSalesOrderService : ISapSalesOrderService
    {
        private readonly ILogger<SapServiceLayerSalesOrderService> _logger;

        public SapServiceLayerSalesOrderService(ILogger<SapServiceLayerSalesOrderService> logger)
        {
            _logger = logger;
        }

        public async Task<int?> CreateSalesOrderAsync(
            Tenant tenant,
            SapSalesOrderDraft order,
            CancellationToken cancellationToken)
        {
            var serviceLayer = tenant.ServiceLayer;
            ValidateConfiguration(tenant);

            using var httpClient = CreateHttpClient(serviceLayer);
            var sessionCookie = await LoginAsync(httpClient, tenant, serviceLayer, cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(serviceLayer, "Orders"));
            request.Headers.Add("Cookie", sessionCookie);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = CreateJsonContent(BuildOrderPayload(order, serviceLayer));

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "SAP Service Layer failed to create sales order for tenant {TenantCode} and customer {CardCode}. StatusCode: {StatusCode}. Response: {ResponseBody}",
                    tenant.Code,
                    order.CardCode,
                    response.StatusCode,
                    responseBody);

                throw new InvalidOperationException("SAP Service Layer rejected the sales order.");
            }

            var createdOrder = await response.Content.ReadFromJsonAsync<ServiceLayerCreatedOrder>(cancellationToken);
            return createdOrder?.DocEntry;
        }

        private async Task<string> LoginAsync(
            HttpClient httpClient,
            Tenant tenant,
            TenantServiceLayerSettings serviceLayer,
            CancellationToken cancellationToken)
        {
            var payload = new
            {
                CompanyDB = string.IsNullOrWhiteSpace(serviceLayer.CompanyDB) ? tenant.Database : serviceLayer.CompanyDB,
                UserName = serviceLayer.UserName,
                Password = serviceLayer.Password
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(serviceLayer, "Login"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = CreateJsonContent(payload);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "SAP Service Layer login failed for tenant {TenantCode}. StatusCode: {StatusCode}. Response: {ResponseBody}",
                    tenant.Code,
                    response.StatusCode,
                    responseBody);

                throw new InvalidOperationException("SAP Service Layer login failed.");
            }

            if (!response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
            {
                _logger.LogError("SAP Service Layer login did not return session cookies for tenant {TenantCode}.", tenant.Code);
                throw new InvalidOperationException("SAP Service Layer login did not return cookies.");
            }

            var sessionCookie = string.Join("; ", setCookieHeaders
                .Select(header => header.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
                .Where(cookie => !string.IsNullOrWhiteSpace(cookie)));

            if (string.IsNullOrWhiteSpace(sessionCookie))
                throw new InvalidOperationException("SAP Service Layer login returned empty cookies.");

            return sessionCookie;
        }

        private static ServiceLayerOrderPayload BuildOrderPayload(
            SapSalesOrderDraft order,
            TenantServiceLayerSettings serviceLayer)
        {
            var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            return new ServiceLayerOrderPayload
            {
                CardCode = order.CardCode,
                DocDate = today,
                DocDueDate = today,
                TaxDate = today,
                Series = serviceLayer.DefaultSeries,
                ShipToCode = order.ShippingAddressName,
                DocumentLines = order.Lines.Select(line => new ServiceLayerOrderLinePayload
                {
                    ItemCode = line.ItemCode,
                    Quantity = line.Quantity,
                    DiscountPercent = line.DiscountPercent,
                    TaxCode = string.IsNullOrWhiteSpace(line.TaxCode) ? null : line.TaxCode,
                    WarehouseCode = serviceLayer.DefaultWarehouse
                }).ToList(),
                Comments = BuildOrderComments(order.Comments),
                U_FromWeb = "Y",
                U_Status = 1
            };
        }

        private static string BuildOrderComments(string? comments)
        {
            const string webOrderComment = "Orden viene de Web";

            if (string.IsNullOrWhiteSpace(comments))
                return webOrderComment;

            return $"{webOrderComment}{Environment.NewLine}{comments.Trim()}";
        }

        private static StringContent CreateJsonContent<TPayload>(TPayload payload)
        {
            var json = JsonSerializer.Serialize(payload);
            return new StringContent(json, encoding: null, mediaType: "application/json");
        }

        private static HttpClient CreateHttpClient(TenantServiceLayerSettings serviceLayer)
        {
            var handler = new HttpClientHandler
            {
                UseCookies = false
            };

            if (serviceLayer.AllowInvalidCertificate)
            {
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            return new HttpClient(handler, disposeHandler: true);
        }

        private static Uri BuildEndpoint(TenantServiceLayerSettings serviceLayer, string resource)
        {
            var baseUrl = serviceLayer.BaseUrl.TrimEnd('/');
            return new Uri($"{baseUrl}/{resource.TrimStart('/')}", UriKind.Absolute);
        }

        private static void ValidateConfiguration(Tenant tenant)
        {
            var serviceLayer = tenant.ServiceLayer;

            if (string.IsNullOrWhiteSpace(serviceLayer.BaseUrl))
                throw new InvalidOperationException($"SAP Service Layer BaseUrl is not configured for tenant {tenant.Code}.");

            if (string.IsNullOrWhiteSpace(serviceLayer.UserName) || string.IsNullOrWhiteSpace(serviceLayer.Password))
                throw new InvalidOperationException($"SAP Service Layer credentials are not configured for tenant {tenant.Code}.");

            if (string.IsNullOrWhiteSpace(serviceLayer.DefaultWarehouse))
                throw new InvalidOperationException($"SAP Service Layer default warehouse is not configured for tenant {tenant.Code}.");
        }

        private sealed class ServiceLayerOrderPayload
        {
            public string CardCode { get; set; } = string.Empty;
            public string DocDate { get; set; } = string.Empty;
            public string DocDueDate { get; set; } = string.Empty;
            public string TaxDate { get; set; } = string.Empty;
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? Series { get; set; }
            public string ShipToCode { get; set; } = string.Empty;
            public List<ServiceLayerOrderLinePayload> DocumentLines { get; set; } = new();
            public string? Comments { get; set; }
            public string U_FromWeb { get; set; }
            public int U_Status { get; set; }
        }

        private sealed class ServiceLayerOrderLinePayload
        {
            public string ItemCode { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public decimal DiscountPercent { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? TaxCode { get; set; }
            public string WarehouseCode { get; set; } = string.Empty;
        }

        private sealed class ServiceLayerCreatedOrder
        {
            public int? DocEntry { get; set; }
        }
    }
}
