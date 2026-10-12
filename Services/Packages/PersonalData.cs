using System.Linq;

namespace CourierService.Services.Packages
{
    /// <summary>
    /// Scan and collection screens show enough to check someone's identity, not everything we hold (DR-004).
    /// </summary>
    public static class PersonalData
    {
        /// <summary>
        /// "0821234567" becomes "*******567". Returns null for a blank number.
        /// </summary>
        public static string MaskPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return null;
            }

            var trimmed = phone.Trim();
            const int keep = 3;

            if (trimmed.Length <= keep)
            {
                return new string('*', trimmed.Length);
            }

            var visible = new string(trimmed.Skip(trimmed.Length - keep).ToArray());
            return new string('*', trimmed.Length - keep) + visible;
        }

        /// <summary>
        /// Masks an email address, preserving its domain and only the first character of the local part.
        /// Invalid or blank addresses are fully masked rather than returned unchanged.
        /// </summary>
        public static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var trimmed = email.Trim();
            var at = trimmed.IndexOf('@');

            if (at <= 0 || at != trimmed.LastIndexOf('@') ||
                at == trimmed.Length - 1)
            {
                return new string('*', trimmed.Length);
            }

            var local = trimmed.Substring(0, at);
            var domain = trimmed.Substring(at + 1);

            if (string.IsNullOrWhiteSpace(domain) ||
                domain.StartsWith(".") ||
                domain.EndsWith("."))
            {
                return new string('*', trimmed.Length);
            }

            return local.Substring(0, 1) + new string('*', local.Length - 1) + "@" + domain;
        }
    }
}
