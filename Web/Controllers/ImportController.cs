using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Domain.Repositories;
using CourierService.Services.Audit;
using CourierService.Services.Packages;
using CourierService.Services.Security;
using CourierService.Web.Infrastructure;

namespace CourierService.Web.Controllers
{
    /// <summary>
    /// Bulk package registration from a CSV file (FR-08; IR-009 to IR-013; NFR-009; docs/API_CONTRACT.md).
    /// Every row goes through PackageRegistrationService.Register, the same service the registration form uses
    /// (IR-009), so a row is accepted or refused by exactly the same rules either way.
    /// </summary>
    public class ImportController : Controller
    {
        private readonly IPackageRegistrationService _registration;
        private readonly IStorageLocationRepository _storageLocations;
        private readonly IAuditLogger _auditLogger;

        public ImportController()
        {
            var connectionFactory = new SqlConnectionFactory();
            _storageLocations = new StorageLocationRepository(connectionFactory);
            _auditLogger = new AuditLogger(new AuditLogRepository(connectionFactory));
            _registration = new PackageRegistrationService(
                new PackageRepository(connectionFactory),
                new PackageRegistrationRepository(),
                new PackageStatusHistoryRepository(connectionFactory),
                _storageLocations,
                new AppConfigRepository(connectionFactory),
                _auditLogger,
                new UnitOfWorkFactory(connectionFactory));
        }

        /// <summary>
        /// POST /api/import/csv: 200 with { importedCount, failedRows: [{ row, reason }] }. Supervisor/SystemAdmin
        /// only (FR-08). The whole file is rejected (nothing imported) if a required column is missing or
        /// renamed (IR-010); otherwise bad rows are skipped and reported, valid rows are imported (IR-011).
        /// </summary>
        [RoleAuthorize(RoleNames.Supervisor, RoleNames.SystemAdmin)]
        [HttpPost]
        [Route("api/import/csv")]
        public ActionResult Csv()
        {
            var userId = Session["UserId"] as int?;
            if (!userId.HasValue)
            {
                return Error(401, "NotAuthenticated", "You need to log in to do this.");
            }

            if (Request.Files.Count == 0 || Request.Files[0] == null || Request.Files[0].ContentLength == 0)
            {
                return Error(400, "ValidationError", "A CSV file is required.");
            }

            string csvText;
            using (var reader = new StreamReader(Request.Files[0].InputStream, Encoding.UTF8))
            {
                csvText = reader.ReadToEnd();
            }

            string[] header;
            List<string[]> dataRows;
            if (!CsvPackageImport.TryParse(csvText, out header, out dataRows))
            {
                return Error(400, "ValidationError", "The file is empty.");
            }

            Dictionary<string, int> columnIndex;
            string missingColumns;
            if (!CsvPackageImport.TryMapHeader(header, out columnIndex, out missingColumns))
            {
                return Error(400, "ValidationError", "The file is missing required column(s): " + missingColumns + ".");
            }

            // Loaded once, not once per row (500 rows in under 60s, NFR-009)
            var locations = _storageLocations.GetAll(activeOnly: false).ToList();

            var failedRows = new List<object>();
            var importedCount = 0;

            for (var i = 0; i < dataRows.Count; i++)
            {
                var rowNumber = i + 2; // row 1 is the header, so the first data row is row 2

                string reason;
                var request = CsvPackageImport.ToRequest(dataRows[i], columnIndex, locations, out reason);
                if (request == null)
                {
                    failedRows.Add(new { row = rowNumber, reason });
                    continue;
                }

                var result = _registration.Register(request, userId.Value);
                if (!result.Success)
                {
                    failedRows.Add(new { row = rowNumber, reason = result.Message });
                    continue;
                }

                importedCount++;
            }

            // One summary entry for the whole file, not one per package (PackageCreated already logs those) (SR-04)
            _auditLogger.Log(
                AuditActions.CsvImported,
                AuditEntityTypes.CsvImport,
                null,
                importedCount + " imported, " + failedRows.Count + " failed, " + dataRows.Count + " total",
                userId.Value);

            return Json(new { importedCount, failedRows });
        }

        private ActionResult Error(int statusCode, string code, string message)
        {
            Response.StatusCode = statusCode;
            Response.TrySkipIisCustomErrors = true;
            return Json(new { error = new { code, message } }, JsonRequestBehavior.AllowGet);
        }
    }
}