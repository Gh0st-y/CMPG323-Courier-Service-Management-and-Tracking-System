using System.Linq;
using System.Text;

namespace CourierService.Services.Users
{
    /// <summary>Password rule for new accounts (T43, NRF-014): at least 8 characters with letters and numbers.</summary>
    public static class PasswordPolicy
    {
        public const int MinLength = 8;

        // BCrypt only looks at the first 72 bytes of a password. Anything longer would be cut without telling
        // anyone, so it is refused instead.
        public const int MaxBytes = 72;

        public const string Requirement =
            "Passwords must be at least 8 characters (at most 72) and contain at least one letter and one number.";

        public static bool IsValid(string password, out string message)
        {
            message = null;

            if (password == null
                || password.Length < MinLength
                || Encoding.UTF8.GetByteCount(password) > MaxBytes
                || !password.Any(char.IsLetter)
                || !password.Any(char.IsDigit))
            {
                message = Requirement;
                return false;
            }

            return true;
        }
    }
}