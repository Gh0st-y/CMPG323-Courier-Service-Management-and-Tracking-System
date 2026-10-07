namespace CourierService.Services.Packages
{
    /// <summary>The outcome of registering a package: the new identifiers, or why it was refused.</summary>
    public class PackageRegistrationResult
    {
        private PackageRegistrationResult()
        {
        }

        public bool Success { get; private set; }

        public int PackageId { get; private set; }
        public string F20Identifier { get; private set; }
        public decimal Fee { get; private set; }
        public string PaymentStatus { get; private set; }

        /// <summary>For a refusal: "ValidationError". Message is safe to show the clerk.</summary>
        public string ErrorCode { get; private set; }
        public string Message { get; private set; }

        public static PackageRegistrationResult Registered(int packageId, string f20Identifier, decimal fee, string paymentStatus)
        {
            return new PackageRegistrationResult
            {
                Success = true,
                PackageId = packageId,
                F20Identifier = f20Identifier,
                Fee = fee,
                PaymentStatus = paymentStatus
            };
        }

        public static PackageRegistrationResult Invalid(string message)
        {
            return new PackageRegistrationResult { Success = false, ErrorCode = "ValidationError", Message = message };
        }
    }
}