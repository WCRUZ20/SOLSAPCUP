using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesPortal.Application.Abstractions.Persistence;
using SalesPortal.Shared.Security;
using SalesPortal.Web.Models.Home;
using System.Globalization;
using System.Security.Claims;

namespace SalesPortal.Web.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    private readonly ITenantResolver _tenantResolver;
    private readonly IOrderRepository _orderRepository;

    public HomeController(
        ITenantResolver tenantResolver,
        IOrderRepository orderRepository)
    {
        _tenantResolver = tenantResolver;
        _orderRepository = orderRepository;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var cardCode = User.FindFirstValue(PortalClaimTypes.CardCode);

        if (string.IsNullOrWhiteSpace(cardCode))
            return RedirectToAction("Login", "Auth");

        var tenant = _tenantResolver.ResolveByHost(HttpContext.Request.Host.Value);
        var today = DateTime.Today;
        var last30Days = today.AddDays(-30);
        var chartStart = new DateTime(today.Year, today.Month, 1).AddMonths(-5);

        var orders = await _orderRepository.GetCustomerOrdersAsync(
            tenant,
            cardCode,
            chartStart,
            today,
            null,
            cancellationToken);

        var products = await _orderRepository.GetSalesItemsAsync(tenant, cardCode, cancellationToken);
        var recentOrders = orders
            .OrderByDescending(order => order.DocDate)
            .ThenByDescending(order => order.DocNum)
            .Take(5)
            .Select(order => new DashboardRecentOrderViewModel
            {
                DocEntry = order.DocEntry,
                DocNum = order.DocNum,
                DocDate = order.DocDate,
                DocTotal = order.DocTotal,
                Status = order.Status,
                ShippingAddress = order.ShippingAddress
            })
            .ToList();

        var ordersLast30Days = orders
            .Where(order => order.DocDate.Date >= last30Days && order.DocDate.Date <= today)
            .ToList();
        var totalAmountLast30Days = ordersLast30Days.Sum(order => order.DocTotal);
        var monthlySales = BuildMonthlySales(orders, chartStart, today);
        var statusSummary = BuildStatusSummary(orders);

        var model = new DashboardViewModel
        {
            TotalOrders = orders.Count,
            OrdersLast30Days = ordersLast30Days.Count,
            TotalAmountLast30Days = totalAmountLast30Days,
            AverageTicketLast30Days = ordersLast30Days.Count == 0 ? 0 : totalAmountLast30Days / ordersLast30Days.Count,
            AvailableProducts = products.Count,
            LastOrderDate = orders.Count == 0 ? null : orders.Max(order => order.DocDate),
            MonthlySales = monthlySales,
            OrdersByStatus = statusSummary,
            RecentOrders = recentOrders
        };

        return View(model);
    }

    private static IReadOnlyList<DashboardMonthAmountViewModel> BuildMonthlySales(
        IReadOnlyList<SalesPortal.Domain.Orders.CustomerOrder> orders,
        DateTime chartStart,
        DateTime today)
    {
        var months = Enumerable.Range(0, 6)
            .Select(offset => new DateTime(chartStart.Year, chartStart.Month, 1).AddMonths(offset))
            .Where(month => month <= new DateTime(today.Year, today.Month, 1))
            .ToList();
        var totalsByMonth = orders
            .GroupBy(order => new DateTime(order.DocDate.Year, order.DocDate.Month, 1))
            .ToDictionary(group => group.Key, group => group.Sum(order => order.DocTotal));
        var maxAmount = totalsByMonth.Count == 0 ? 0 : totalsByMonth.Values.Max();

        return months
            .Select(month =>
            {
                totalsByMonth.TryGetValue(month, out var amount);

                return new DashboardMonthAmountViewModel
                {
                    Label = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(month.ToString("MMM", CultureInfo.CurrentCulture)),
                    Amount = amount,
                    Percentage = maxAmount <= 0 ? 0 : Math.Max(8, (int)Math.Round(amount / maxAmount * 100))
                };
            })
            .ToList();
    }

    private static IReadOnlyList<DashboardStatusSummaryViewModel> BuildStatusSummary(
        IReadOnlyList<SalesPortal.Domain.Orders.CustomerOrder> orders)
    {
        var totalOrders = orders.Count;

        return orders
            .GroupBy(order => string.IsNullOrWhiteSpace(order.Status) ? "Sin estado" : order.Status.Trim())
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Take(5)
            .Select(group => new DashboardStatusSummaryViewModel
            {
                Status = group.Key,
                Count = group.Count(),
                Percentage = totalOrders == 0 ? 0 : Math.Max(6, (int)Math.Round(group.Count() / (decimal)totalOrders * 100))
            })
            .ToList();
    }
}
