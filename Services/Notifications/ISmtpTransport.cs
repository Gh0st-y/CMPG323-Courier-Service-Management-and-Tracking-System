using System.Net;
using System.Net.Mail;

namespace CourierService.Services.Notifications
{
    /// <summary>Hands a finished email to the mail server. Separate from the sender so tests can stand in for the server.</summary>
    public interface ISmtpTransport
    {
        void Send(MailMessage message, SmtpSettings settings);
    }

    /// <summary>The real transport: System.Net.Mail.SmtpClient (tech spec 5.2.1, FR-09).</summary>
    public class SmtpClientTransport : ISmtpTransport
    {
        public void Send(MailMessage message, SmtpSettings settings)
        {
            using (var client = new SmtpClient(settings.Host, settings.Port))
            {
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.EnableSsl = settings.UseSsl;

                // SmtpClient waits 100 s by default; a dead server shouldn't hold the worker that long
                client.Timeout = settings.TimeoutSeconds * 1000;

                if (string.IsNullOrEmpty(settings.UserName))
                {
                    client.UseDefaultCredentials = false;
                }
                else
                {
                    client.Credentials = new NetworkCredential(settings.UserName, settings.Password);
                }

                client.Send(message);
            }
        }
    }
}