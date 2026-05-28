using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SalesPortal.Shared.Security;

namespace SalesPortal.Web.Filters;

public sealed class RequirePasswordChangeFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;

        var isAuthenticated = httpContext.User.Identity?.IsAuthenticated == true;

        if (!isAuthenticated)
        {
            await next();
            return;
        }

        var mustChangePassword = httpContext.User.Claims
            .FirstOrDefault(x => x.Type == PortalClaimTypes.MustChangePassword)
            ?.Value == "Y";

        if (!mustChangePassword)
        {
            await next();
            return;
        }

        var controller = context.RouteData.Values["controller"]?.ToString() ?? string.Empty;
        var action = context.RouteData.Values["action"]?.ToString() ?? string.Empty;

        var isAuthController = controller.Equals("Auth", StringComparison.OrdinalIgnoreCase);

        var isAllowedAction =
            isAuthController &&
            (
                action.Equals("ChangePassword", StringComparison.OrdinalIgnoreCase) ||
                action.Equals("Logout", StringComparison.OrdinalIgnoreCase)
            );

        if (isAllowedAction)
        {
            await next();
            return;
        }

        context.Result = new RedirectToActionResult(
            "ChangePassword",
            "Auth",
            routeValues: null);
    }
}