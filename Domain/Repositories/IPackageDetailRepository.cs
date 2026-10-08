using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CourierService.Domain.Models;

namespace CourierService.Domain.Repositories
{
    /// <summary>T22: supporting data for the package detail view (DR-012).</summary>
    public interface IPackageDetailRepository
    {
        /// <summary>Oldest first, so the timeline reads top-to-bottom in order.</summary>
        IEnumerable<PackageStatusHistoryItem> GetStatusHistory(int packageId);

        /// <summary>Newest first. Cross-referenced to the package via PackageId (DR-012).</summary>
        IEnumerable<NotificationLogItem> GetNotificationLog(int packageId);
    }
}
