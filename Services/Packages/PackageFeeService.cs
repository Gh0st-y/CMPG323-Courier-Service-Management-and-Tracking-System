using System;
using System.Globalization;
using CourierService.Domain.Repositories;

namespace CourierService.Services.Packages
{
    /// <summary>Reads current database fees for both registration and the staff fee display.</summary>
    public class PackageFeeService
    {
        private readonly IAppConfigRepository _config;
        public PackageFeeService(IAppConfigRepository config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }
        public decimal GetFee(string classification)
        {
            if (classification != PackageRegistrationService.Personal && classification != PackageRegistrationService.WorkRelated)
                throw new ArgumentException("Unknown package classification.", nameof(classification));
            var key = "Fee." + classification;
            var raw = _config.GetValue(key);
            decimal fee;
            // Packages.Fee is decimal(6,2). Reject values the database would round or overflow.
            if (raw == null || !decimal.TryParse(raw.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out fee)
                || fee < 0 || fee > 9999.99m || fee != decimal.Round(fee, 2))
                throw new InvalidOperationException("dbo.AppConfig has no valid value for " + key + ".");
            return fee;
        }
    }
}
