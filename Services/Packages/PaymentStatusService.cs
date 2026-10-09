using System;
using CourierService.Domain;
using CourierService.Domain.Models;
using CourierService.Domain.Repositories;
using CourierService.Services.Audit;

namespace CourierService.Services.Packages
{
    /// <summary>The three payment statuses as stored in dbo.Packages.PaymentStatus (FR-15, FR-17).</summary>
    public static class PaymentStatuses
    {
        public const string Paid = "Paid";
        public const string Unpaid = "Unpaid";
        public const string Exempt = "Exempt";

        /// <summary>"paid", " Paid " give "Paid"; anything other than the three is refused.</summary>
        public static bool TryNormalize(string value, out string status)
        {
            var cleaned = (value ?? string.Empty).Trim();
            foreach (var known in new[] { Paid, Unpaid, Exempt })
            {
                if (string.Equals(cleaned, known, StringComparison.OrdinalIgnoreCase))
                {
                    status = known;
                    return true;
                }
            }

            status = null;
            return false;
        }

        /// <summary>The status a new package starts with: work-related is Exempt, personal is Unpaid (T41).</summary>
        public static string DefaultFor(string classification)
        {
            return string.Equals(classification, "WorkRelated", StringComparison.OrdinalIgnoreCase) ? Exempt : Unpaid;
        }
    }

    public enum PaymentChangeOutcome
    {
        Changed,

        /// <summary>The package already had that status; nothing was written.</summary>
        Unchanged,
        PackageNotFound
    }

    public class PaymentChangeResult
    {
        public PaymentChangeOutcome Outcome { get; set; }

        /// <summary>The package's payment details after the call (null for PackageNotFound).</summary>
        public PaymentInfo Payment { get; set; }

        public string F20Identifier { get; set; }
    }

    /// <summary>
    /// Staff recording a payment status (T41, FR-17): stores the status with the time and the staff member, and writes
    /// a PaymentStatusChanged audit entry in the same transaction (SR-04). Every earlier change stays in the audit log,
    /// which is the record for reconciliation with finance (FR-15). Any status can be set from any other, at any stage
    /// of the package, because a fee is often paid at collection and recorded afterwards.
    /// </summary>
    public class PaymentStatusService
    {
        private readonly IPackageRepository _packages;
        private readonly IPackagePaymentRepository _payments;
        private readonly IAuditLogger _auditLogger;
        private readonly IUnitOfWorkFactory _unitOfWorkFactory;

        public PaymentStatusService(
            IPackageRepository packages,
            IPackagePaymentRepository payments,
            IAuditLogger auditLogger,
            IUnitOfWorkFactory unitOfWorkFactory)
        {
            if (packages == null) throw new ArgumentNullException(nameof(packages));
            if (payments == null) throw new ArgumentNullException(nameof(payments));
            if (auditLogger == null) throw new ArgumentNullException(nameof(auditLogger));
            if (unitOfWorkFactory == null) throw new ArgumentNullException(nameof(unitOfWorkFactory));

            _packages = packages;
            _payments = payments;
            _auditLogger = auditLogger;
            _unitOfWorkFactory = unitOfWorkFactory;
        }

        /// <param name="f20Identifier">Already normalised (PackageIdentifier.TryNormalize).</param>
        /// <param name="paymentStatus">Already normalised (PaymentStatuses.TryNormalize).</param>
        public PaymentChangeResult Change(string f20Identifier, string paymentStatus, int changedByUserId)
        {
            var package = _packages.GetByF20Identifier(f20Identifier);
            if (package == null)
            {
                return new PaymentChangeResult { Outcome = PaymentChangeOutcome.PackageNotFound };
            }

            if (string.Equals(package.PaymentStatus, paymentStatus, StringComparison.OrdinalIgnoreCase))
            {
                return new PaymentChangeResult
                {
                    Outcome = PaymentChangeOutcome.Unchanged,
                    F20Identifier = package.F20Identifier,
                    Payment = _payments.GetPaymentInfo(package.PackageId)
                        ?? new PaymentInfo { PackageId = package.PackageId, PaymentStatus = package.PaymentStatus }
                };
            }

            using (var unitOfWork = _unitOfWorkFactory.Begin())
            {
                if (!_payments.UpdatePaymentStatus(package.PackageId, paymentStatus, changedByUserId, unitOfWork))
                {
                    return new PaymentChangeResult { Outcome = PaymentChangeOutcome.PackageNotFound };
                }

                _auditLogger.Log(
                    AuditActions.PaymentStatusChanged,
                    AuditEntityTypes.Package,
                    package.F20Identifier,
                    package.PaymentStatus + " to " + paymentStatus + " (fee R" + package.Fee.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ")",
                    changedByUserId,
                    unitOfWork);

                var payment = _payments.GetPaymentInfo(package.PackageId, unitOfWork);
                unitOfWork.Commit();

                return new PaymentChangeResult
                {
                    Outcome = PaymentChangeOutcome.Changed,
                    F20Identifier = package.F20Identifier,
                    Payment = payment
                };
            }
        }
    }
}
