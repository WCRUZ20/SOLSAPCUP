using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SalesPortal.Application.Abstractions.Authentication;
using SalesPortal.Application.Auth.Dtos;
using SalesPortal.Shared.Security;
using SalesPortal.Web.Models.Auth;
using SalesPortal.Web.Services;
using System.Security.Claims;

namespace SalesPortal.Web.Controllers
{
    public sealed class AuthController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IRegistrationEmailSender _registrationEmailSender;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IAuthService authService,
            IRegistrationEmailSender registrationEmailSender,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _registrationEmailSender = registrationEmailSender;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return View(model);

            var host = HttpContext.Request.Host.Value;

            var result = await _authService.LoginAsync(
                new LoginRequest
                {
                    UserOrEmail = model.UserOrEmail,
                    Password = model.Password,
                    Host = host
                },
                cancellationToken);

            if (result.IsFailure || result.Data == null)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return View(model);
            }

            var login = result.Data;

            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, login.CardCode),
            new Claim(ClaimTypes.Name, login.CardName),
            new Claim(ClaimTypes.Email, login.EmailWeb ?? string.Empty),
            new Claim(PortalClaimTypes.CardCode, login.CardCode),
            new Claim(PortalClaimTypes.CardName, login.CardName),
            new Claim(PortalClaimTypes.UserWeb, login.UserWeb),
            new Claim(PortalClaimTypes.TenantCode, login.TenantCode),
            new Claim(PortalClaimTypes.MustChangePassword, login.MustChangePassword ? "Y" : "N"),
            new Claim(PortalClaimTypes.Role, login.Role),
            new Claim(ClaimTypes.Role, login.Role == "A" ? "Admin" : "Player")
        };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    AllowRefresh = true
                });

            if (login.CountryWasAssigned)
            {
                TempData["AssignedCountryName"] = login.AssignedCountryName;
                TempData["AssignedCountryCode"] = login.AssignedCountryCode;
            }

            TempData["ShowLoginNotices"] = "true";

            if (login.MustChangePassword)
                return RedirectToAction(nameof(ChangePassword));

            return RedirectToAction("Index", "Home");
        }


        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            RegisterViewModel model,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return View(model);

            var host = HttpContext.Request.Host.Value;

            var result = await _authService.RegisterAsync(
                new RegisterRequest
                {
                    IdentificationNumber = model.IdentificationNumber,
                    FirstName = model.FirstName,
                    FirstLastName = model.FirstLastName,
                    Email = model.Email,
                    Host = host
                },
                cancellationToken);

            if (result.IsFailure || result.Data == null)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return View(model);
            }

            var registeredUser = result.Data;
            var fullName = $"{model.FirstName.Trim()} {model.FirstLastName.Trim()}";

            try
            {
                await _registrationEmailSender.SendCredentialsAsync(
                    fullName,
                    model.Email,
                    registeredUser.UserWeb,
                    registeredUser.DefaultPassword,
                    cancellationToken);

                TempData["Success"] = "Registro creado correctamente. Las credenciales de acceso fueron enviadas al correo electrónico registrado.";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "No se pudo enviar el correo de credenciales para el usuario {UserWeb}.", registeredUser.UserWeb);
                TempData["Success"] = $"Registro creado correctamente. Usuario: {registeredUser.UserWeb}. Contraseña temporal: {registeredUser.DefaultPassword}.";
                TempData["Warning"] = "No se pudo enviar el correo con las credenciales. Verifique la configuración SMTP.";
            }

            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            if (User.Identity?.IsAuthenticated != true)
                return RedirectToAction(nameof(Login));

            var cardCode = User.FindFirstValue(PortalClaimTypes.CardCode);

            if (string.IsNullOrWhiteSpace(cardCode))
                return RedirectToAction(nameof(Login));

            return View(new ChangePasswordViewModel
            {
                CardCode = cardCode
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            ChangePasswordViewModel model,
            CancellationToken cancellationToken)
        {
            if (User.Identity?.IsAuthenticated != true)
                return RedirectToAction(nameof(Login));

            if (!ModelState.IsValid)
                return View(model);

            var host = HttpContext.Request.Host.Value;

            var result = await _authService.ChangePasswordAsync(
                new ChangePasswordRequest
                {
                    CardCode = model.CardCode,
                    NewPassword = model.NewPassword,
                    ConfirmPassword = model.ConfirmPassword,
                    Host = host
                },
                cancellationToken);

            if (result.IsFailure)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return View(model);
            }

            await RefreshClaimsAfterPasswordChangeAsync();

            TempData["Success"] = "Contraseña actualizada correctamente.";
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        private async Task RefreshClaimsAfterPasswordChangeAsync()
        {
            var currentClaims = User.Claims
                .Where(x => x.Type != PortalClaimTypes.MustChangePassword)
                .ToList();

            currentClaims.Add(new Claim(PortalClaimTypes.MustChangePassword, "N"));

            var identity = new ClaimsIdentity(
                currentClaims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    AllowRefresh = true
                });
        }
    }
}
