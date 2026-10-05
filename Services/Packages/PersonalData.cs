using System.Linq;

namespace CourierService.Services.Packages
{
    /// <summary>
    /// Scan and collection screens show enough to check someone's identity, not everything we hold (DR-004).
    /// </summary>
    public static class PersonalData
    {
        /// <summary>
        /// "0821234567" becomes "*******567": the last three digits are enough for a recipient to confirm
        /// it's theirs, and not enough to be useful to anyone else. Returns null for a blank number.
        /// </summary>
        public static string MaskPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return null;
            }

            var trimmed = phone.Trim();
            var keep = 3;

            if (trimmed.Length <= keep)
            {
                return new string('*', trimmed.Length);
            }

            var visible = new string(trimmed.Skip(trimmed.Length - keep).ToArray());
            return new string('*', trimmed.Length - keep) + visible;
        }
    }
}
