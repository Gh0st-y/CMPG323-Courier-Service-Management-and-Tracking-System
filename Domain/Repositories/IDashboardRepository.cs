using System.Collections.Generic;
using CourierService.Domain.Models;

namespace CourierService.Domain.Repositories
{
    public interface IDashboardRepository
    {
        DashboardStats GetStats(string period = "today");

        /// <summary>Most recent status changes, newest first.</summary>
        IEnumerable<RecentActivityItem> GetRecentActivity(int count = 10);
    }
}