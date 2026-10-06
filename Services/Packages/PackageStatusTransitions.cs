using System.Collections.Generic;
using CourierService.Domain.Entities;

namespace CourierService.Services.Packages
{
    /// <summary>
    /// The rules for moving a package between statuses (FR-04, DR-009). This is the single place
    /// they are defined: DECISIONS.md #7 keeps it to four states in one straight line,
    ///
    ///     Registered -> In Storage -> Ready for Collection -> Collected
    ///
    /// No skipping ahead, no going back, and Collected is the end. Pure logic with no database
    /// access, so every pair can be tested directly. The UI may hide invalid options, but this is
    /// what actually enforces it.
    /// </summary>
    public static class PackageStatusTransitions
    {
        private static readonly IReadOnlyList<PackageStatus> None = new PackageStatus[0];

        public static bool IsAllowed(PackageStatus from, PackageStatus to)
        {
            foreach (var next in AllowedNext(from))
            {
                if (next == to)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The statuses a package can move to from <paramref name="from"/>. Empty for Collected.</summary>
        public static IReadOnlyList<PackageStatus> AllowedNext(PackageStatus from)
        {
            switch (from)
            {
                case PackageStatus.Registered:
                    return new[] { PackageStatus.InStorage };
                case PackageStatus.InStorage:
                    return new[] { PackageStatus.ReadyForCollection };
                case PackageStatus.ReadyForCollection:
                    return new[] { PackageStatus.Collected };
                default:
                    return None;
            }
        }

        /// <summary>Staff-facing name, matching what the screens show ("In Storage", not "InStorage").</summary>
        public static string DisplayName(PackageStatus status)
        {
            switch (status)
            {
                case PackageStatus.InStorage:
                    return "In Storage";
                case PackageStatus.ReadyForCollection:
                    return "Ready for Collection";
                default:
                    return status.ToString();
            }
        }

        /// <summary>The message for a rejected change. The first form matches the example in docs/API_CONTRACT.md.</summary>
        public static string RejectionMessage(PackageStatus from, PackageStatus to)
        {
            return from == to
                ? "Package is already " + DisplayName(from) + "."
                : "Package cannot move to " + DisplayName(to) + " from " + DisplayName(from) + ".";
        }
    }
}
