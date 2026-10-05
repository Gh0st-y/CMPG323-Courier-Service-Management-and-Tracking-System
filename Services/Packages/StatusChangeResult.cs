using System.Collections.Generic;
using CourierService.Domain.Entities;

namespace CourierService.Services.Packages
{
    public enum StatusChangeOutcome
    {
        Changed,

        /// <summary>No package has that identifier. The API turns this into the friendly 404 (IR-006).</summary>
        NotFound,

        /// <summary>The move isn't allowed from the package's current status. The API turns this into a 409 (DR-009).</summary>
        InvalidTransition,

        /// <summary>
        /// Someone else changed the package between us reading it and saving. Nothing was written.
        /// The API turns this into a 409; the user should reload and try again.
        /// </summary>
        ConcurrentUpdate,

        /// <summary>Something the caller sent is wrong, e.g. a storage location that doesn't exist. The API turns this into a 400.</summary>
        InvalidInput
    }

    /// <summary>What happened to a status change request. Expected business outcomes come back here rather than as exceptions.</summary>
    public class StatusChangeResult
    {
        public StatusChangeOutcome Outcome { get; private set; }
        public bool Success { get { return Outcome == StatusChangeOutcome.Changed; } }

        /// <summary>Same code names as the error shape in docs/API_CONTRACT.md. Null when it worked.</summary>
        public string ErrorCode { get; private set; }

        /// <summary>A message that is safe to show staff. Null when it worked.</summary>
        public string Message { get; private set; }

        public string F20Identifier { get; private set; }

        /// <summary>The status the package had when we read it. Null when the package wasn't found.</summary>
        public PackageStatus? FromStatus { get; private set; }

        public PackageStatus ToStatus { get; private set; }

        /// <summary>For InvalidTransition: what the package can move to instead, so the UI can say so.</summary>
        public IReadOnlyList<PackageStatus> AllowedNextStatuses { get; private set; }

        private StatusChangeResult()
        {
            AllowedNextStatuses = new PackageStatus[0];
        }

        public static StatusChangeResult Changed(string f20Identifier, PackageStatus from, PackageStatus to)
        {
            return new StatusChangeResult
            {
                Outcome = StatusChangeOutcome.Changed,
                F20Identifier = f20Identifier,
                FromStatus = from,
                ToStatus = to
            };
        }

        public static StatusChangeResult NotFound(string f20Identifier, PackageStatus to)
        {
            return new StatusChangeResult
            {
                Outcome = StatusChangeOutcome.NotFound,
                ErrorCode = "NotFound",
                Message = "No package was found for that identifier.",
                F20Identifier = f20Identifier,
                ToStatus = to
            };
        }

        public static StatusChangeResult InvalidTransition(string f20Identifier, PackageStatus from, PackageStatus to)
        {
            return new StatusChangeResult
            {
                Outcome = StatusChangeOutcome.InvalidTransition,
                ErrorCode = "InvalidTransition",
                Message = PackageStatusTransitions.RejectionMessage(from, to),
                F20Identifier = f20Identifier,
                FromStatus = from,
                ToStatus = to,
                AllowedNextStatuses = PackageStatusTransitions.AllowedNext(from)
            };
        }

        public static StatusChangeResult InvalidInput(string f20Identifier, PackageStatus to, string message)
        {
            return new StatusChangeResult
            {
                Outcome = StatusChangeOutcome.InvalidInput,
                ErrorCode = "ValidationError",
                Message = message,
                F20Identifier = f20Identifier,
                ToStatus = to
            };
        }

        public static StatusChangeResult ConcurrentUpdate(string f20Identifier, PackageStatus from, PackageStatus to)
        {
            return new StatusChangeResult
            {
                Outcome = StatusChangeOutcome.ConcurrentUpdate,
                ErrorCode = "ConcurrentUpdate",
                Message = "This package was just changed by someone else. Reload it and try again.",
                F20Identifier = f20Identifier,
                FromStatus = from,
                ToStatus = to
            };
        }
    }
}
