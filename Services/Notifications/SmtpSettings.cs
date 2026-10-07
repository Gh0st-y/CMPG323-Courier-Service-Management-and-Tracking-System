using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Net.Mail;

namespace CourierService.Services.Notifications
{
    /// <summary>
    /// The SMTP settings from Web.config appSettings (T06, T26). Only Smtp.Host and Smtp.FromAddress are needed; the
    /// rest have defaults. Never commit a real Smtp.Password: put it in Web.config.Local, which git ignores.
    ///
    ///   Smtp.Host            (required)  e.g. localhost for smtp4dev or Papercut during development
    ///   Smtp.Port            25
    ///   Smtp.FromAddress     (required)  e.g. courier-noreply@f20.local
    ///   Smtp.FromName        F20 Courier Service
    ///   Smtp.UseSsl          false
    ///   Smtp.UserName        (none)      only when the server needs a login
    ///   Smtp.Password        (none)
    ///   Smtp.TimeoutSeconds  10          how long to wait for the server before counting a failed attempt
    /// </summary>
    public class SmtpSettings
    {
        public const int DefaultPort = 25;
        public const int DefaultTimeoutSeconds = 10;
        public const string DefaultFromName = PlainTextNotificationComposer.ServiceName;

        public string Host { get; set; }
        public int Port { get; set; } = DefaultPort;
        public string FromAddress { get; set; }
        public string FromName { get; set; } = DefaultFromName;
        public bool UseSsl { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

        public static SmtpSettings FromAppSettings(NameValueCollection appSettings)
        {
            if (appSettings == null) throw new ArgumentNullException(nameof(appSettings));

            return new SmtpSettings
            {
                Host = Clean(appSettings["Smtp.Host"]),
                Port = Number(appSettings["Smtp.Port"], DefaultPort, 1, 65535),
                FromAddress = Clean(appSettings["Smtp.FromAddress"]),
                FromName = Clean(appSettings["Smtp.FromName"]) ?? DefaultFromName,
                UseSsl = string.Equals(Clean(appSettings["Smtp.UseSsl"]), "true", StringComparison.OrdinalIgnoreCase),
                UserName = Clean(appSettings["Smtp.UserName"]),
                Password = appSettings["Smtp.Password"],
                TimeoutSeconds = Number(appSettings["Smtp.TimeoutSeconds"], DefaultTimeoutSeconds, 1, 300)
            };
        }

        /// <summary>What is wrong with the settings, or null if they can be used.</summary>
        public string Problem()
        {
            if (string.IsNullOrWhiteSpace(Host))
            {
                return "Smtp.Host is not set.";
            }

            if (string.IsNullOrWhiteSpace(FromAddress))
            {
                return "Smtp.FromAddress is not set.";
            }

            try
            {
                new MailAddress(FromAddress);
            }
            catch (FormatException)
            {
                return "Smtp.FromAddress is not a valid email address.";
            }

            return null;
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static int Number(string value, int fallback, int min, int max)
        {
            int number;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) && number >= min && number <= max
                ? number
                : fallback;
        }
    }
}