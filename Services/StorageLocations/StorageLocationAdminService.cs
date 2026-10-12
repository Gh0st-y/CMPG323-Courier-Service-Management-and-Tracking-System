using System;
using System.Linq;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;
using CourierService.Services.Audit;

namespace CourierService.Services.StorageLocations
{
    public enum CreateLocationOutcome
    {
        Created,

        /// <summary>Code missing, or code or description too long. Message says which.</summary>
        Invalid,

        /// <summary>A location with that code already exists, active or not (codes are unique in the database).</summary>
        DuplicateCode
    }

    public class CreateLocationResult
    {
        public CreateLocationOutcome Outcome { get; set; }
        public string Message { get; set; }
        public StorageLocation Location { get; set; }
    }

    /// <summary>
    /// Adding a storage location (T24). SystemAdmin only, checked by the controller (SR-02). The new location and its
    /// audit entry are saved in one transaction (SR-04), so there is never a location without a record of who added it.
    /// </summary>
    public class StorageLocationAdminService
    {
        // Column sizes in dbo.StorageLocations
        public const int MaxCodeLength = 50;
        public const int MaxDescriptionLength = 200;

        private readonly IStorageLocationRepository _locations;
        private readonly IAuditLogger _auditLogger;
        private readonly IUnitOfWorkFactory _unitOfWorkFactory;

        public StorageLocationAdminService(
            IStorageLocationRepository locations,
            IAuditLogger auditLogger,
            IUnitOfWorkFactory unitOfWorkFactory)
        {
            if (locations == null) throw new ArgumentNullException(nameof(locations));
            if (auditLogger == null) throw new ArgumentNullException(nameof(auditLogger));
            if (unitOfWorkFactory == null) throw new ArgumentNullException(nameof(unitOfWorkFactory));

            _locations = locations;
            _auditLogger = auditLogger;
            _unitOfWorkFactory = unitOfWorkFactory;
        }

        public CreateLocationResult Create(string code, string description, int createdByUserId)
        {
            var cleanCode = (code ?? string.Empty).Trim();
            var cleanDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

            if (cleanCode.Length == 0)
            {
                return Invalid("Code is required.");
            }

            if (cleanCode.Length > MaxCodeLength)
            {
                return Invalid("Code must be " + MaxCodeLength + " characters or fewer.");
            }

            if (cleanDescription != null && cleanDescription.Length > MaxDescriptionLength)
            {
                return Invalid("Description must be " + MaxDescriptionLength + " characters or fewer.");
            }

            // Includes inactive ones: the UNIQUE constraint does too, and the database compares without case
            if (_locations.GetAll(false).Any(l => string.Equals(l.Code, cleanCode, StringComparison.OrdinalIgnoreCase)))
            {
                return new CreateLocationResult
                {
                    Outcome = CreateLocationOutcome.DuplicateCode,
                    Message = "A storage location with that code already exists."
                };
            }

            var location = new StorageLocation { Code = cleanCode, Description = cleanDescription, IsActive = true };

            using (var unitOfWork = _unitOfWorkFactory.Begin())
            {
                location.StorageLocationId = _locations.Insert(location, unitOfWork);

                // A location code is not personal data (SR-03)
                _auditLogger.Log(
                    AuditActions.StorageLocationCreated,
                    AuditEntityTypes.StorageLocation,
                    location.StorageLocationId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "Storage location " + cleanCode + " added",
                    createdByUserId,
                    unitOfWork);

                unitOfWork.Commit();
            }

            return new CreateLocationResult { Outcome = CreateLocationOutcome.Created, Location = location };
        }

        private static CreateLocationResult Invalid(string message)
        {
            return new CreateLocationResult { Outcome = CreateLocationOutcome.Invalid, Message = message };
        }
    }
}
