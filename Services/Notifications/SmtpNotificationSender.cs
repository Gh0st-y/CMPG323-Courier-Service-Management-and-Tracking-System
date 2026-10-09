using System;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;

namespace CourierService.Services.Notifications
{
    /// <summary>
    /// Sends email notifications through SMTP (T26, FR-09). The background worker (T25) calls it, never a staff request,
    /// so a slow or switched-off mail server only delays the email (NFR-022).
    ///
    /// It never throws: every problem comes back as a failed result. Problems that won't fix themselves (an address
    /// that isn't valid, a mailbox the server says doesn't exist) are permanent, so the worker stops retrying.
    /// Everything else (server down, busy, timed out, not set up yet) is worth another try.
    ///
    /// Error texts never contain the recipient's address or name (SR-03). That is why exception messages are not used:
    /// the server's reply that SmtpException repeats can include the address.
    /// </summary>
    public class SmtpNotificationSender : INotificationSender
    {
        private readonly SmtpSettings _settings;
        private readonly ISmtpTransport _transport;

        public SmtpNotificationSender(SmtpSettings settings, ISmtpTransport transport = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _settings = settings;
            _transport = transport ?? new SmtpClientTransport();
        }

        public string Channel => NotificationChannels.Email;

        public NotificationSendResult Send(NotificationMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            var problem = _settings.Problem();
            if (problem != null)
            {
                // A settings mistake: worth retrying, because fixing Web.config restarts the app and the next attempt works
                return NotificationSendResult.Failed("Email is not set up: " + problem);
            }

            MailAddress to;
            try
            {
                to = new MailAddress((message.To ?? string.Empty).Trim());
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
            {
                return NotificationSendResult.Failed("The recipient's email address is not valid.", permanent: true);
            }

            try
            {
                using (var mail = new MailMessage())
                {
                    mail.From = new MailAddress(_settings.FromAddress, _settings.FromName);
                    mail.To.Add(to);
                    mail.Subject = message.Subject ?? string.Empty;
                    mail.Body = message.Body ?? string.Empty;
                    mail.IsBodyHtml = false;
                    mail.SubjectEncoding = Encoding.UTF8;
                    mail.BodyEncoding = Encoding.UTF8;

                    _transport.Send(mail, _settings);
                }

                return NotificationSendResult.Sent();
            }
            catch (SmtpFailedRecipientException ex)
            {
                // The server answered, but about this recipient: a 5xx answer means it will never accept them
                var code = (int)ex.StatusCode;
                var permanent = code >= 500 && code < 600;
                return NotificationSendResult.Failed(
                    (permanent ? "The mail server rejected the recipient" : "The mail server could not take the email for now")
                    + " (" + code + " " + ex.StatusCode + ").",
                    permanent);
            }
            catch (SmtpException ex)
            {
                return NotificationSendResult.Failed("The mail server could not be used (" + Describe(ex) + ").");
            }
            catch (Exception ex)
            {
                return NotificationSendResult.Failed("Sending the email failed (" + ex.GetType().Name + ").");
            }
        }

        // e.g. "GeneralFailure, ConnectionRefused" when nothing is listening on the port. Codes only, never text.
        private static string Describe(SmtpException ex)
        {
            var description = ex.StatusCode == SmtpStatusCode.GeneralFailure
                ? ex.StatusCode.ToString()
                : (int)ex.StatusCode + " " + ex.StatusCode;

            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
            {
                var socket = inner as SocketException;
                if (socket != null)
                {
                    return description + ", " + socket.SocketErrorCode;
                }
            }

            return ex.InnerException == null ? description : description + ", " + ex.InnerException.GetType().Name;
        }
    }
}