using Microsoft.Extensions.Options;
using SalesPortal.Web.Options;
using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;

namespace SalesPortal.Web.Services
{
    public sealed class SmtpRegistrationEmailSender : IRegistrationEmailSender
    {
        private readonly SmtpOptions _options;

        public SmtpRegistrationEmailSender(IOptions<SmtpOptions> options)
        {
            _options = options.Value;
        }

        public async Task SendCredentialsAsync(
            string fullName,
            string email,
            string userName,
            string temporaryPassword,
            CancellationToken cancellationToken)
        {
            if (!_options.Enabled)
                throw new InvalidOperationException("Configure Smtp:Enabled en true para enviar las credenciales por correo.");

            ValidateOptions();

            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromEmail.Trim(), _options.FromName.Trim()),
                Subject = "Credenciales de acceso a SOLSAPCUP",
                Body = BuildPlainTextBody(fullName, email, userName, temporaryPassword),
                IsBodyHtml = false
            };

            message.To.Add(new MailAddress(email.Trim(), fullName.Trim()));
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                BuildHtmlBody(fullName, email, userName, temporaryPassword),
                null,
                "text/html"));

            using var client = new SmtpClient(_options.Host.Trim(), _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrWhiteSpace(_options.UserName))
            {
                client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
            }

            using var registration = cancellationToken.Register(client.SendAsyncCancel);
            await client.SendMailAsync(message, cancellationToken);
        }

        private void ValidateOptions()
        {
            if (string.IsNullOrWhiteSpace(_options.Host))
                throw new InvalidOperationException("Configure Smtp:Host para enviar las credenciales por correo.");

            if (string.IsNullOrWhiteSpace(_options.FromEmail))
                throw new InvalidOperationException("Configure Smtp:FromEmail para enviar las credenciales por correo.");
        }

        private static string BuildPlainTextBody(
            string fullName,
            string email,
            string userName,
            string temporaryPassword)
        {
            return $"""
                Hola {fullName},

                Tu usuario fue creado correctamente en SOLSAPCUP.

                Información para iniciar sesión:
                Usuario: {userName}
                Correo electrónico: {email}
                Contraseña temporal: {temporaryPassword}

                Por seguridad, el sistema solicitará cambiar esta contraseña en el primer ingreso.
                """;
        }

        private static string BuildHtmlBody(
            string fullName,
            string email,
            string userName,
            string temporaryPassword)
        {
            var encoder = HtmlEncoder.Default;

            return $"""
                <!doctype html>
                <html lang="es">
                <body style="font-family: Arial, sans-serif; color: #212529; line-height: 1.5;">
                    <p>Hola {encoder.Encode(fullName)},</p>
                    <p>Tu usuario fue creado correctamente en <strong>SOLSAPCUP</strong>.</p>
                    <p>Información para iniciar sesión:</p>
                    <ul>
                        <li><strong>Usuario:</strong> {encoder.Encode(userName)}</li>
                        <li><strong>Correo electrónico:</strong> {encoder.Encode(email)}</li>
                        <li><strong>Contraseña temporal:</strong> {encoder.Encode(temporaryPassword)}</li>
                    </ul>
                    <p>Por seguridad, el sistema solicitará cambiar esta contraseña en el primer ingreso.</p>
                </body>
                </html>
                """;
        }
    }
}
