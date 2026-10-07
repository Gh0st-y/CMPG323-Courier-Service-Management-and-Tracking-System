using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;
using CourierService.Services.Audit;

namespace CourierService.Services.Packages
{
    public class PackageRegistrationService : IPackageRegistrationService
    {
        public const string Personal = "Personal";
        public const string WorkRelated = "WorkRelated";

        // The options on the registration form
        public static readonly string[] PackageTypes = { "Envelope", "Box", "Parcel", "Other" };

        // Column sizes in schema.sql
        private const int MaxFullName = 150, MaxIdentifierNo = 50, MaxEmail = 200, MaxPhone = 30,
            MaxDepartment = 150, MaxSenderName = 150, MaxNotes = 500;

        // A sanity check, not full RFC validation (same rule as user management)
        private static readonly Regex EmailPattern = new Regex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$");

        // After spaces, dashes, dots and brackets are removed: an optional + and 9 to 15 digits,
        // e.g. 0821234567 or +27821234567
        private static readonly Regex PhonePattern = new Regex(@"^\+?[0-9]{9,15}$");
        private static readonly Regex PhoneSeparators = new Regex(@"[\s\-\.\(\)]");

        private readonly IPackageRepository _packages;
        private readonly IPackageRegistrationRepository _registration;
        private readonly IPackageStatusHistoryRepository _history;
        private readonly IStorageLocationRepository _storageLocations;
        private readonly IAppConfigRepository _config;
        private readonly IAuditLogger _auditLogger;
        private readonly IUnitOfWorkFactory _unitOfWorkFactory;

        public PackageRegistrationService(
            IPackageRepository packages,
            IPackageRegistrationRepository registration,
            IPackageStatusHistoryRepository history,
            IStorageLocationRepository storageLocations,
            IAppConfigRepository config,
            IAuditLogger auditLogger,
            IUnitOfWorkFactory unitOfWorkFactory)
        {
            if (packages == null) throw new ArgumentNullException(nameof(packages));
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            if (history == null) throw new ArgumentNullException(nameof(history));
            if (storageLocations == null) throw new ArgumentNullException(nameof(storageLocations));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (auditLogger == null) throw new ArgumentNullException(nameof(auditLogger));
            if (unitOfWorkFactory == null) throw new ArgumentNullException(nameof(unitOfWorkFactory));

            _packages = packages;
            _registration = registration;
            _history = history;
            _storageLocations = storageLocations;
            _config = config;
            _auditLogger = auditLogger;
            _unitOfWorkFactory = unitOfWorkFactory;
        }

        /// <summary>"F20-" and the number, at least four digits: 1 becomes F20-0001, 60001 becomes F20-60001.</summary>
        public static string FormatF20Identifier(int number)
        {
            return "F20-" + number.ToString("0000", CultureInfo.InvariantCulture);
        }

        public PackageRegistrationResult Register(PackageRegistrationRequest request, int registeredByUserId)
        {
            if (request == null)
            {
                return PackageRegistrationResult.Invalid("The package details are required.");
            }

            // ---- Check everything before anything is written (UR-03) ----

            var fullName = Clean(request.RecipientFullName);
            var identifierNo = Clean(request.RecipientIdentifierNo);
            var email = Clean(request.RecipientEmail);
            var phone = Clean(request.RecipientPhone);
            var department = Clean(request.RecipientDepartment);
            var senderName = Clean(request.SenderName);
            var notes = Clean(request.Notes);

            if (fullName == null) return PackageRegistrationResult.Invalid("Recipient full name is required.");
            if (fullName.Length > MaxFullName) return TooLong("Recipient full name", MaxFullName);

            if (identifierNo == null) return PackageRegistrationResult.Invalid("Staff / student number is required.");
            if (identifierNo.Length > MaxIdentifierNo) return TooLong("Staff / student number", MaxIdentifierNo);

            if (email == null) return PackageRegistrationResult.Invalid("Recipient email is required.");
            if (email.Length > MaxEmail || !EmailPattern.IsMatch(email))
            {
                return PackageRegistrationResult.Invalid("Recipient email doesn't look valid.");
            }

            if (phone == null) return PackageRegistrationResult.Invalid("Recipient phone is required.");
            phone = PhoneSeparators.Replace(phone, string.Empty);
            if (phone.Length > MaxPhone || !PhonePattern.IsMatch(phone))
            {
                return PackageRegistrationResult.Invalid("Recipient phone must be 9 to 15 digits, e.g. 0821234567.");
            }

            if (department != null && department.Length > MaxDepartment) return TooLong("Department", MaxDepartment);

            if (senderName == null) return PackageRegistrationResult.Invalid("Sender name is required.");
            if (senderName.Length > MaxSenderName) return TooLong("Sender name", MaxSenderName);

            var packageType = PackageTypes.FirstOrDefault(t => string.Equals(t, Clean(request.PackageType), StringComparison.OrdinalIgnoreCase));
            if (packageType == null)
            {
                return PackageRegistrationResult.Invalid("Package type must be one of: " + string.Join(", ", PackageTypes) + ".");
            }

            string classification;
            if (!TryNormalizeClassification(request.Classification, out classification))
            {
                return PackageRegistrationResult.Invalid("Classification must be Personal or WorkRelated.");
            }

            if (!request.StorageLocationId.HasValue)
            {
                return PackageRegistrationResult.Invalid("Storage location is required.");
            }

            var location = _storageLocations.GetById(request.StorageLocationId.Value);
            if (location == null || !location.IsActive)
            {
                return PackageRegistrationResult.Invalid("That storage location doesn't exist or is no longer in use.");
            }

            if (notes != null && notes.Length > MaxNotes) return TooLong("Notes", MaxNotes);

            // The fee comes from dbo.AppConfig (Fee.Personal / Fee.WorkRelated), so it can change without a redeploy (FR-15)
            var fee = ReadFee(classification);

            // Nothing to pay means nothing to chase (FR-17): work-related packages are Exempt, the rest start Unpaid
            var paymentStatus = fee == 0m ? "Exempt" : "Unpaid";

            // ---- Save: identifier, recipient, package, history and audit together, or not at all ----

            int packageId;
            string f20Identifier;

            using (var unitOfWork = _unitOfWorkFactory.Begin())
            {
                f20Identifier = FormatF20Identifier(_registration.NextF20Number(unitOfWork));

                var recipientId = _registration.InsertRecipient(new Recipient
                {
                    FullName = fullName,
                    IdentifierNo = identifierNo,
                    Email = email,
                    PhoneNumber = phone,
                    Department = department
                }, unitOfWork);

                packageId = _packages.Insert(new Package
                {
                    F20Identifier = f20Identifier,
                    RecipientId = recipientId,
                    SenderName = senderName,
                    PackageType = packageType,
                    Classification = classification,
                    Fee = fee,
                    PaymentStatus = paymentStatus,
                    Status = PackageStatus.Registered,
                    StorageLocationId = location.StorageLocationId,
                    Notes = notes,
                    CreatedByUserId = registeredByUserId
                }, unitOfWork);

                // The first line of the package's timeline (DR-012)
                _history.Insert(new PackageStatusHistoryEntry
                {
                    PackageId = packageId,
                    FromStatus = null,
                    ToStatus = PackageStatus.Registered,
                    ChangedByUserId = registeredByUserId,
                    ChangedAtUtc = DateTime.UtcNow
                }, unitOfWork);

                // No names or contact details in the audit log (SR-03)
                _auditLogger.Log(
                    AuditActions.PackageCreated,
                    AuditEntityTypes.Package,
                    f20Identifier,
                    classification + ", fee " + fee.ToString("0.00", CultureInfo.InvariantCulture),
                    registeredByUserId,
                    unitOfWork);

                unitOfWork.Commit();
            }

            return PackageRegistrationResult.Registered(packageId, f20Identifier, fee, paymentStatus);
        }

        // Accepts "Personal", "WorkRelated", "Work-related" or "work related", in any case
        private static bool TryNormalizeClassification(string value, out string classification)
        {
            var letters = new string((value ?? string.Empty).Where(char.IsLetter).ToArray()).ToLowerInvariant();

            classification = letters == "personal" ? Personal
                : letters == "workrelated" ? WorkRelated
                : null;

            return classification != null;
        }

        private decimal ReadFee(string classification)
        {
            var key = "Fee." + classification;
            var raw = _config.GetValue(key);

            decimal fee;
            if (raw == null || !decimal.TryParse(raw.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out fee) || fee < 0)
            {
                // A setup problem (db/schema.sql seeds these), not something the clerk can fix
                throw new InvalidOperationException("dbo.AppConfig has no valid value for " + key + ".");
            }

            return fee;
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static PackageRegistrationResult TooLong(string field, int max)
        {
            return PackageRegistrationResult.Invalid(field + " can be at most " + max + " characters.");
        }
    }
}