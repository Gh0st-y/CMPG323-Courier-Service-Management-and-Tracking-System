using System;
using CourierService.Domain.Entities;

namespace CourierService.Services.Packages
{
    /// <summary>Turns the text a caller sends as "newStatus" into a <see cref="PackageStatus"/>.</summary>
    public static class PackageStatusParser
    {
        /// <summary>
        /// Accepts the names used in the API ("InStorage", "ReadyForCollection") and the staff-facing ones
        /// ("In Storage", "Ready for Collection"), in any letter case. Does NOT accept numbers: Enum.TryParse
        /// would turn "1" into a status, which is never what a caller meant.
        /// </summary>
        public static bool TryParse(string text, out PackageStatus status)
        {
            status = default(PackageStatus);

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var compact = text.Replace(" ", string.Empty).Trim();

            foreach (var name in Enum.GetNames(typeof(PackageStatus)))
            {
                if (string.Equals(name, compact, StringComparison.OrdinalIgnoreCase))
                {
                    status = (PackageStatus)Enum.Parse(typeof(PackageStatus), name);
                    return true;
                }
            }

            return false;
        }
    }
}
