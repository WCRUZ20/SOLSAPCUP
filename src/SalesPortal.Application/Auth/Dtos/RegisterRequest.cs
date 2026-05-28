namespace SalesPortal.Application.Auth.Dtos
{
    public sealed class RegisterRequest
    {
        public string IdentificationNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string FirstLastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
    }
}
