namespace SalesPortal.Application.Auth.Dtos
{
    public sealed class PortalCustomerRegistration
    {
        public string CardCode { get; set; } = string.Empty;
        public string CardName { get; set; } = string.Empty;
        public string IdentificationNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string UserWeb { get; set; } = string.Empty;
        public string PasswordWeb { get; set; } = string.Empty;
        public string Dealer { get; set; } = string.Empty;
        public string MustChangePassword { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
