namespace SalesPortal.Application.Auth.Dtos
{
    public sealed class RegisterResult
    {
        public string CardCode { get; set; } = string.Empty;
        public string UserWeb { get; set; } = string.Empty;
        public string DefaultPassword { get; set; } = string.Empty;
    }
}
