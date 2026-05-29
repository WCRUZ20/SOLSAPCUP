namespace SalesPortal.Web.Services
{
    public interface IRegistrationEmailSender
    {
        Task SendCredentialsAsync(
            string fullName,
            string email,
            string userName,
            string temporaryPassword,
            CancellationToken cancellationToken);
    }
}
