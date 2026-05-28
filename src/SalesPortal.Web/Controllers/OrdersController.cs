using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SalesPortal.Application.Abstractions.Persistence;
using SalesPortal.Application.Abstractions.Sap;
using SalesPortal.Shared.Security;
using SalesPortal.Web.Models.Orders;
using SalesPortal.Web.Services.Orders;
using System.IO;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SalesPortal.Web.Controllers
{
    [Authorize]
    [Route("Pedidos")]
    public sealed class OrdersController : Controller
    {
        private readonly ITenantResolver _tenantResolver;
        private readonly IOrderRepository _orderRepository;
        private readonly ISapSalesOrderService _sapSalesOrderService;
        private readonly IOrderSubmissionCoordinator _orderSubmissionCoordinator;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            ITenantResolver tenantResolver,
            IOrderRepository orderRepository,
            ISapSalesOrderService sapSalesOrderService,
            IOrderSubmissionCoordinator orderSubmissionCoordinator,
            ILogger<OrdersController> logger)
        {
            _tenantResolver = tenantResolver;
            _orderRepository = orderRepository;
            _sapSalesOrderService = sapSalesOrderService;
            _orderSubmissionCoordinator = orderSubmissionCoordinator;
            _logger = logger;
        }


        [HttpGet("Crear")]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            var cardCode = User.FindFirstValue(PortalClaimTypes.CardCode);

            if (string.IsNullOrWhiteSpace(cardCode))
                return RedirectToAction("Login", "Auth");

            var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);
            var items = await _orderRepository.GetSalesItemsAsync(tenant, cardCode, cancellationToken);
            var addresses = await _orderRepository.GetShippingAddressesAsync(tenant, cardCode, cancellationToken);

            var model = new OrderCreateViewModel
            {
                Items = items.Select(item => new SalesItemOptionViewModel
                {
                    ItemCode = item.ItemCode,
                    FrgnName = item.FrgnName,
                    CategoryName = item.CategoryName,
                    UnitPrice = item.UnitPrice,
                    DiscountPercent = item.DiscountPercent,
                    TaxCode = item.TaxCode
                }).ToList(),
                SubmissionToken = Guid.NewGuid().ToString("N"),
                ShippingAddresses = addresses.Select(address => new ShippingAddressOptionViewModel
                {
                    AddressName = address.AddressName,
                    FullAddress = address.FullAddress
                }).ToList()
            };

            return View(model);
        }

        [HttpPost("Confirmar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(
            [FromForm] string orderPayload,
            [FromForm] string submissionToken,
            CancellationToken cancellationToken)
        {
            var cardCode = User.FindFirstValue(PortalClaimTypes.CardCode);

            if (string.IsNullOrWhiteSpace(cardCode))
                return RedirectToAction("Login", "Auth");

            ConfirmOrderRequest? request;

            try
            {
                request = JsonSerializer.Deserialize<ConfirmOrderRequest>(
                    orderPayload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                request = null;
            }

            if (request is null || string.IsNullOrWhiteSpace(request.ShippingAddressName) || request.Lines.Count == 0)
            {
                TempData["Error"] = "Complete la dirección de envío y agregue al menos una línea antes de confirmar el pedido.";
                return RedirectToAction(nameof(Create));
            }

            request.Comments = request.Comments?.Trim();

            if (request.Comments?.Length > 500)
            {
                TempData["Error"] = "El comentario no puede exceder 500 caracteres.";
                return RedirectToAction(nameof(Create));
            }

            if (request.Lines.Any(line => string.IsNullOrWhiteSpace(line.ItemCode) || line.Quantity <= 0))
            {
                TempData["Error"] = "Todas las líneas deben tener artículo y cantidad mayor a cero.";
                return RedirectToAction(nameof(Create));
            }

            var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);
            var addresses = await _orderRepository.GetShippingAddressesAsync(tenant, cardCode, cancellationToken);
            var items = await _orderRepository.GetSalesItemsAsync(tenant, cardCode, cancellationToken);

            var validAddress = addresses.Any(address => address.AddressName == request.ShippingAddressName);
            var itemsByCode = items.ToDictionary(item => item.ItemCode, StringComparer.OrdinalIgnoreCase);

            if (!validAddress || request.Lines.Any(line => !itemsByCode.ContainsKey(line.ItemCode)))
            {
                TempData["Error"] = "El pedido contiene una dirección o un artículo no disponible para el socio de negocio.";
                return RedirectToAction(nameof(Create));
            }

            if (string.IsNullOrWhiteSpace(submissionToken) || !Guid.TryParseExact(submissionToken, "N", out _))
            {
                TempData["Error"] = "No se pudo validar la confirmación del pedido. Vuelva a intentarlo.";
                return RedirectToAction(nameof(Create));
            }

            var submissionKey = BuildSubmissionKey(tenant.Code, cardCode, request);
            OrderSubmissionExecutionResult submissionResult;

            try
            {
                var orderDraft = new SapSalesOrderDraft
                {
                    CardCode = cardCode,
                    ShippingAddressName = request.ShippingAddressName,
                    Comments = request.Comments,
                    Lines = request.Lines.Select(line =>
                    {
                        var item = itemsByCode[line.ItemCode];

                        return new SapSalesOrderDraftLine
                        {
                            ItemCode = line.ItemCode,
                            Quantity = line.Quantity,
                            DiscountPercent = item.DiscountPercent,
                            TaxCode = item.TaxCode
                        };
                    }).ToList()
                };

                submissionResult = await _orderSubmissionCoordinator.ExecuteOnceAsync(
                    submissionKey,
                    operationToken => _sapSalesOrderService.CreateSalesOrderAsync(tenant, orderDraft, operationToken),
                    cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error creating SAP sales order for customer {CardCode}.", cardCode);
                TempData["Error"] = "Error al crear el pedido";
                return RedirectToAction(nameof(Create));
            }

            switch (submissionResult.Status)
            {
                case OrderSubmissionExecutionStatus.DuplicateInProgress:
                    TempData["Error"] = "El pedido ya se está procesando. Espere la confirmación antes de intentarlo nuevamente.";
                    return RedirectToAction(nameof(Create));

                case OrderSubmissionExecutionStatus.DuplicateCompleted:
                    TempData["Success"] = "Este pedido ya había sido confirmado; no se creó una orden duplicada.";
                    TempData["ClearOrderDraft"] = "true";
                    return RedirectToAction(nameof(Index));

                default:
                    TempData["Success"] = "Pedido confirmado correctamente.";
                    TempData["ClearOrderDraft"] = "true";
                    return RedirectToAction(nameof(Index));
            }
        }


        private static bool IsExcelFile(string fileName)
        {
            return string.Equals(Path.GetExtension(fileName), ".xlsx", StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<BulkOrderRowViewModel> BuildBulkOrders(
            BulkOrderWorkbook workbook,
            IEnumerable<string> addressNames,
            IEnumerable<SalesPortal.Domain.Orders.SalesItem> salesItems)
        {
            var validAddresses = addressNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var itemsByCode = salesItems.ToDictionary(item => item.ItemCode, StringComparer.OrdinalIgnoreCase);
            var lineRecordsByOrder = workbook.Lines
                .GroupBy(line => line.IdOrden.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.OrderBy(line => line.Linea).ToList(), StringComparer.OrdinalIgnoreCase);
            var duplicateHeaderIds = workbook.Headers
                .GroupBy(header => header.IdOrden.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var orders = new List<BulkOrderRowViewModel>();

            foreach (var header in workbook.Headers)
            {
                var order = new BulkOrderRowViewModel
                {
                    IdOrden = header.IdOrden,
                    ShippingAddressName = header.Direccion,
                    Comments = header.Comentario
                };

                if (string.IsNullOrWhiteSpace(header.IdOrden))
                    order.ValidationMessages.Add($"Cabecera fila {header.RowNumber}: IdOrden es obligatorio.");

                if (duplicateHeaderIds.Contains(header.IdOrden))
                    order.ValidationMessages.Add($"IdOrden {header.IdOrden} está duplicado en la pestaña Cabecera.");

                if (string.IsNullOrWhiteSpace(header.Direccion))
                    order.ValidationMessages.Add($"IdOrden {header.IdOrden}: Dirección es obligatoria.");
                else if (!validAddresses.Contains(header.Direccion))
                    order.ValidationMessages.Add($"IdOrden {header.IdOrden}: la dirección no está disponible para el socio de negocio.");

                if ((header.Comentario?.Length ?? 0) > 500)
                    order.ValidationMessages.Add($"IdOrden {header.IdOrden}: el comentario no puede exceder 500 caracteres.");

                if (!lineRecordsByOrder.TryGetValue(header.IdOrden.Trim(), out var lineRecords) || lineRecords.Count == 0)
                {
                    order.ValidationMessages.Add($"IdOrden {header.IdOrden}: debe incluir al menos una línea en la pestaña Detalle.");
                }
                else
                {
                    foreach (var lineRecord in lineRecords)
                    {
                        var line = new BulkOrderLineViewModel
                        {
                            LineNumber = lineRecord.Linea,
                            ItemCode = lineRecord.Articulo,
                            Quantity = lineRecord.Cantidad
                        };

                        if (lineRecord.Linea <= 0)
                            order.ValidationMessages.Add($"Detalle fila {lineRecord.RowNumber}: Linea debe ser mayor a cero.");

                        if (string.IsNullOrWhiteSpace(lineRecord.Articulo))
                        {
                            order.ValidationMessages.Add($"Detalle fila {lineRecord.RowNumber}: Articulo(sku) es obligatorio.");
                        }
                        else if (itemsByCode.TryGetValue(lineRecord.Articulo, out var item))
                        {
                            line.Description = item.FrgnName;
                            line.UnitPrice = item.UnitPrice;
                            line.DiscountPercent = item.DiscountPercent;
                            line.TaxCode = item.TaxCode;
                            line.LineTotal = Math.Round(line.Quantity * item.UnitPrice * (1 - (item.DiscountPercent / 100)), 2);
                        }
                        else
                        {
                            order.ValidationMessages.Add($"Detalle fila {lineRecord.RowNumber}: el artículo {lineRecord.Articulo} no está disponible para el socio de negocio.");
                        }

                        if (lineRecord.Cantidad <= 0)
                            order.ValidationMessages.Add($"Detalle fila {lineRecord.RowNumber}: Cantidad debe ser mayor a cero.");

                        order.Lines.Add(line);
                    }
                }

                order.EstimatedTotal = order.Lines.Sum(line => line.LineTotal);
                order.Result = order.ValidationMessages.Count == 0 ? BulkOrderResultStatuses.NotProcessed : BulkOrderResultStatuses.Error;
                order.ResultMessage = order.ValidationMessages.Count == 0 ? null : string.Join(" ", order.ValidationMessages);
                orders.Add(order);
            }

            var headerIds = workbook.Headers.Select(header => header.IdOrden.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var orphanLineGroup in workbook.Lines
                .Where(line => !string.IsNullOrWhiteSpace(line.IdOrden) && !headerIds.Contains(line.IdOrden.Trim()))
                .GroupBy(line => line.IdOrden.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                orders.Add(new BulkOrderRowViewModel
                {
                    IdOrden = orphanLineGroup.Key,
                    Result = BulkOrderResultStatuses.Error,
                    ResultMessage = $"IdOrden {orphanLineGroup.Key}: existe en Detalle pero no tiene cabecera.",
                    ValidationMessages = new List<string> { $"IdOrden {orphanLineGroup.Key}: existe en Detalle pero no tiene cabecera." }
                });
            }

            return orders;
        }

        private static List<string> ValidateBulkOrderForProcessing(
            BulkOrderRowViewModel order,
            ISet<string> validAddresses,
            IReadOnlyDictionary<string, SalesPortal.Domain.Orders.SalesItem> itemsByCode)
        {
            var validationMessages = new List<string>();

            if (string.IsNullOrWhiteSpace(order.IdOrden))
                validationMessages.Add("IdOrden es obligatorio.");

            if (string.IsNullOrWhiteSpace(order.ShippingAddressName) || !validAddresses.Contains(order.ShippingAddressName.Trim()))
                validationMessages.Add("La dirección no está disponible para el socio de negocio.");

            if ((order.Comments?.Length ?? 0) > 500)
                validationMessages.Add("El comentario no puede exceder 500 caracteres.");

            if (order.Lines.Count == 0)
                validationMessages.Add("Debe incluir al menos una línea.");

            foreach (var line in order.Lines)
            {
                if (string.IsNullOrWhiteSpace(line.ItemCode) || !itemsByCode.ContainsKey(line.ItemCode.Trim()))
                    validationMessages.Add($"El artículo {line.ItemCode} no está disponible para el socio de negocio.");

                if (line.Quantity <= 0)
                    validationMessages.Add($"La línea {line.LineNumber} debe tener cantidad mayor a cero.");
            }

            return validationMessages;
        }

        private static string BuildSubmissionKey(string tenantCode, string cardCode, ConfirmOrderRequest request)
        {
            var normalizedOrder = new StringBuilder()
                .Append(tenantCode.Trim().ToUpperInvariant())
                .Append('|')
                .Append(cardCode.Trim().ToUpperInvariant())
                .Append('|')
                .Append(request.ShippingAddressName.Trim().ToUpperInvariant())
                .Append('|')
                .Append(request.Comments?.Trim() ?? string.Empty);

            var normalizedLines = request.Lines
                .GroupBy(line => line.ItemCode.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    ItemCode = group.Key.ToUpperInvariant(),
                    Quantity = group.Sum(line => line.Quantity)
                })
                .OrderBy(line => line.ItemCode, StringComparer.Ordinal);

            foreach (var line in normalizedLines)
            {
                normalizedOrder
                    .Append('|')
                    .Append(line.ItemCode)
                    .Append(':')
                    .Append(line.Quantity.ToString(CultureInfo.InvariantCulture));
            }

            var fingerprint = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedOrder.ToString()));
            return Convert.ToHexString(fingerprint);
        }

        [HttpGet("CargaMasiva")]
        public IActionResult BulkUpload()
        {
            return View();
        }

        [HttpGet("CargaMasiva/Plantilla")]
        public IActionResult DownloadBulkUploadTemplate()
        {
            var template = BulkOrderWorkbookService.CreateTemplate();
            return File(
                template,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "PlantillaCargaMasivaPedidos.xlsx");
        }

        [HttpPost("CargaMasiva/Validar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ValidateBulkUpload([FromForm] IFormFile? excelFile, CancellationToken cancellationToken)
        {
            var cardCode = User.FindFirstValue(PortalClaimTypes.CardCode);

            if (string.IsNullOrWhiteSpace(cardCode))
                return Unauthorized();

            if (excelFile is null || excelFile.Length == 0)
            {
                return BadRequest(new BulkOrderUploadResponse
                {
                    Errors = new[] { "Seleccione un archivo Excel para validar." }
                });
            }

            if (!IsExcelFile(excelFile.FileName))
            {
                return BadRequest(new BulkOrderUploadResponse
                {
                    Errors = new[] { "Solo se permiten archivos Excel con extensión .xlsx." }
                });
            }

            BulkOrderWorkbook workbook;

            try
            {
                workbook = BulkOrderWorkbookService.Read(excelFile.OpenReadStream());
            }
            catch (InvalidDataException exception)
            {
                return BadRequest(new BulkOrderUploadResponse
                {
                    Errors = new[] { exception.Message }
                });
            }

            var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);
            var addresses = await _orderRepository.GetShippingAddressesAsync(tenant, cardCode, cancellationToken);
            var items = await _orderRepository.GetSalesItemsAsync(tenant, cardCode, cancellationToken);

            var orders = BuildBulkOrders(workbook, addresses.Select(address => address.AddressName), items);
            return Json(new BulkOrderUploadResponse { Orders = orders });
        }

        [HttpPost("CargaMasiva/Procesar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessBulkUpload([FromBody] BulkOrderProcessRequest request, CancellationToken cancellationToken)
        {
            var cardCode = User.FindFirstValue(PortalClaimTypes.CardCode);

            if (string.IsNullOrWhiteSpace(cardCode))
                return Unauthorized();

            if (request.Orders.Count == 0)
            {
                return BadRequest(new BulkOrderProcessResponse
                {
                    Results = Array.Empty<BulkOrderProcessResultViewModel>()
                });
            }

            var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);
            var addresses = await _orderRepository.GetShippingAddressesAsync(tenant, cardCode, cancellationToken);
            var items = await _orderRepository.GetSalesItemsAsync(tenant, cardCode, cancellationToken);
            var validAddresses = addresses.Select(address => address.AddressName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var itemsByCode = items.ToDictionary(item => item.ItemCode, StringComparer.OrdinalIgnoreCase);
            var results = new List<BulkOrderProcessResultViewModel>();

            foreach (var order in request.Orders.Where(order => order.Result != BulkOrderResultStatuses.Created))
            {
                var validationMessages = ValidateBulkOrderForProcessing(order, validAddresses, itemsByCode);

                if (validationMessages.Count > 0)
                {
                    results.Add(new BulkOrderProcessResultViewModel
                    {
                        IdOrden = order.IdOrden,
                        Result = BulkOrderResultStatuses.Error,
                        Message = string.Join(" ", validationMessages)
                    });
                    continue;
                }

                try
                {
                    var confirmedOrder = new ConfirmOrderRequest
                    {
                        ShippingAddressName = order.ShippingAddressName.Trim(),
                        Comments = order.Comments?.Trim(),
                        Lines = order.Lines.Select(line => new ConfirmOrderLineRequest
                        {
                            ItemCode = line.ItemCode.Trim(),
                            Quantity = line.Quantity
                        }).ToList()
                    };

                    var submissionKey = BuildSubmissionKey(tenant.Code, cardCode, confirmedOrder);
                    var orderDraft = new SapSalesOrderDraft
                    {
                        CardCode = cardCode,
                        ShippingAddressName = confirmedOrder.ShippingAddressName,
                        Comments = confirmedOrder.Comments,
                        Lines = confirmedOrder.Lines.Select(line =>
                        {
                            var item = itemsByCode[line.ItemCode];

                            return new SapSalesOrderDraftLine
                            {
                                ItemCode = line.ItemCode,
                                Quantity = line.Quantity,
                                DiscountPercent = item.DiscountPercent,
                                TaxCode = item.TaxCode
                            };
                        }).ToList()
                    };

                    var submissionResult = await _orderSubmissionCoordinator.ExecuteOnceAsync(
                        submissionKey,
                        operationToken => _sapSalesOrderService.CreateSalesOrderAsync(tenant, orderDraft, operationToken),
                        cancellationToken);

                    var message = submissionResult.Status switch
                    {
                        OrderSubmissionExecutionStatus.DuplicateInProgress => "El pedido ya se está procesando.",
                        OrderSubmissionExecutionStatus.DuplicateCompleted => "Este pedido ya había sido confirmado; no se creó una orden duplicada.",
                        _ => "Pedido creado correctamente."
                    };

                    results.Add(new BulkOrderProcessResultViewModel
                    {
                        IdOrden = order.IdOrden,
                        Result = submissionResult.Status == OrderSubmissionExecutionStatus.DuplicateInProgress
                            ? BulkOrderResultStatuses.Error
                            : BulkOrderResultStatuses.Created,
                        Message = message,
                        DocEntry = submissionResult.DocEntry
                    });
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Error creating SAP sales order from bulk upload for customer {CardCode} and external order {IdOrden}.", cardCode, order.IdOrden);

                    results.Add(new BulkOrderProcessResultViewModel
                    {
                        IdOrden = order.IdOrden,
                        Result = BulkOrderResultStatuses.Error,
                        Message = "Error al crear el pedido."
                    });
                }
            }

            return Json(new BulkOrderProcessResponse { Results = results });
        }

        [HttpGet("{docEntry:int}/Previsualizar")]
        public async Task<IActionResult> Preview(int docEntry, CancellationToken cancellationToken)
        {
            var cardCode = User.FindFirstValue(PortalClaimTypes.CardCode);

            if (string.IsNullOrWhiteSpace(cardCode))
                return RedirectToAction("Login", "Auth");

            var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);
            var order = await _orderRepository.GetCustomerOrderDetailAsync(
                tenant,
                cardCode,
                docEntry,
                cancellationToken);

            if (order is null)
                return NotFound();

            var model = new OrderPreviewViewModel
            {
                DocEntry = order.DocEntry,
                DocNum = order.DocNum,
                DocDate = order.DocDate,
                DocTotal = order.DocTotal,
                Comments = order.Comments,
                ShippingAddress = order.ShippingAddress,
                Status = order.Status,
                Lines = order.Lines.Select(line => new OrderPreviewLineViewModel
                {
                    ItemCode = line.ItemCode,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountPercent = line.DiscountPercent,
                    LineTotal = line.LineTotal
                }).ToList()
            };

            return View(model);
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(
            [FromQuery] OrderListViewModel filters,
            CancellationToken cancellationToken)
        {
            var cardCode = User.FindFirstValue(PortalClaimTypes.CardCode);

            if (string.IsNullOrWhiteSpace(cardCode))
                return RedirectToAction("Login", "Auth");

            if (filters.DateFrom.HasValue && filters.DateTo.HasValue && filters.DateFrom.Value.Date > filters.DateTo.Value.Date)
            {
                ModelState.AddModelError(nameof(filters.DateTo), "La fecha hasta debe ser mayor o igual a la fecha desde.");
                filters.Orders = Array.Empty<OrderListItemViewModel>();
                return View(filters);
            }

            var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);
            var orders = await _orderRepository.GetCustomerOrdersAsync(
                tenant,
                cardCode,
                filters.DateFrom,
                filters.DateTo,
                filters.DocNum,
                cancellationToken);

            filters.Orders = orders
                .Select(order => new OrderListItemViewModel
                {
                    DocEntry = order.DocEntry,
                    DocNum = order.DocNum,
                    DocDate = order.DocDate,
                    DocTotal = order.DocTotal,
                    Comments = order.Comments,
                    ShippingAddress = order.ShippingAddress,
                    Status = order.Status
                })
                .ToList();

            return View(filters);
        }
    }
}
