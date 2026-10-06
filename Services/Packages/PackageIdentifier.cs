using System.Text.RegularExpressions;

namespace CourierService.Services.Packages
{
    /// <summary>
    /// Checks what came out of a scanner or a QR code before it is used to look a package up (IR-006).
    /// The QR code holds only the F20 identifier (CON-008), so anything else is "malformed" and is treated
    /// exactly like an unknown package: a friendly not-found, never an error.
    /// </summary>
    public static class PackageIdentifier
    {
        // Matches dbo.Packages.F20Identifier (NVARCHAR(30)). Letters, digits, hyphen and underscore only: that covers
        // identifiers like F20-0001 and rules out whitespace tricks, quotes and other odd input.
        public const int MaxLength = 30;

        private static readonly Regex WellFormed = new Regex(@"^[A-Za-z0-9_-]{1,30}\z", RegexOptions.Compiled);

        /// <summary>
        /// Trims scanner noise (HID scanners often add a trailing newline or space) and checks the shape.
        /// Returns false for blank, too long, or containing anything other than letters, digits, - and _.
        /// </summary>
        public static bool TryNormalize(string scanned, out string identifier)
        {
            identifier = null;

            if (scanned == null)
            {
                return false;
            }

            var trimmed = scanned.Trim();
            if (!WellFormed.IsMatch(trimmed))
            {
                return false;
            }

            identifier = trimmed;
            return true;
        }
    }
}
