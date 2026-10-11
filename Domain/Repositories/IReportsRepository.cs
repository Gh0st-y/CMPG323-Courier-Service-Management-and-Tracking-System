using CourierService.Domain.Models;

namespace CourierService.Domain.Repositories
{
    public interface IReportsRepository
    {
        /// <summary>Builds everything the Reports page shows for the given filter (defaults to the last 30 days).</summary>
        ReportData GetReport(ReportFilter filter);
    }
}