using System;

namespace CourierService.Services.Packages
{
    /// <summary>What happened to a collection request, and when it was recorded.</summary>
    public class CollectionResult
    {
        public CollectionResult(StatusChangeResult change, DateTime? collectedAtUtc)
        {
            Change = change;
            CollectedAtUtc = collectedAtUtc;
        }

        public StatusChangeResult Change { get; private set; }
        public bool Success { get { return Change.Success; } }

        /// <summary>The time the database stamped on the package. Null unless the collection went through.</summary>
        public DateTime? CollectedAtUtc { get; private set; }
    }

    public interface IPackageCollectionService
    {
        /// <summary>
        /// Hands a package over (FR-07, FR-14). Only works when it is Ready for Collection; the state machine
        /// rejects everything else. Records the time, who verified the collector's identity and which staff
        /// member processed it, and writes the history and audit entries in the same transaction.
        /// </summary>
        /// <param name="processedByUserId">The staff member doing the hand-over (the logged-in user).</param>
        /// <param name="verifiedByUserId">The staff member who checked the collector's ID. Defaults to the processor.
        /// If given it must be an active user, otherwise the result is an InvalidInput.</param>
        CollectionResult Collect(string f20Identifier, int processedByUserId, int? verifiedByUserId = null);
    }
}
