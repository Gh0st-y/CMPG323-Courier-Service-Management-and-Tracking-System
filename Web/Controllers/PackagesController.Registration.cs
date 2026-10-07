using System.Web;
using System.Web.Mvc;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Services.Audit;
using CourierService.Services.Packages;
using CourierService.Services.Security;
using CourierService.Web.Infrastructure;
using CourierService.Web.Models.Api.Packages;

namespace CourierService.Web.Controllers
{
    /// <summary>
    /// Package registration and the label's QR image (FR-03, FR-15, FR-17, CON-008; docs/API_CONTRACT.md).
    /// This is the other half of PackagesController (it is a partial class). POST /api/packages has to live in the
    /// same controller as GET /api/packages: MVC refuses a URL that matches attribute routes on two different
    /// controllers, even when the HTTP methods differ.
    /// </summary>
    public partial class PackagesController
    {
        private IPackageRegistrationService _registration;

        // Built on first use, so the constructors in the main file (and anything that calls them) stay as they are
        private IPackageRegistrationService Registration
        {
            get
            {
                if (_registration == null)
                {
                    var connectionFactory = new SqlConnectionFactory();
                    _registration = new PackageRegistrationService(
                        new PackageRepository(connectionFactory),
                        new PackageRegistrationRepository(),
                        new PackageStatusHistoryRepository(connectionFactory),
                        new StorageLocationRepository(connectionFactory),
                        new AppConfigRepository(connectionFactory),
                        new AuditLogger(new AuditLogRepository(connectionFactory)),
                        new UnitOfWorkFactory(connectionFactory));
                }

                return _registration;
            }
        }

        /// <summary>POST /api/packages: registers a package. 201 with { packageId, f20Identifier, fee, status }.</summary>
        [RoleAuthorize(RoleNames.IntakeClerk, RoleNames.Supervisor, RoleNames.SystemAdmin)]
        [HttpPost]
        [Route("api/packages")]
        public ActionResult RegisterJson()
        {
            var userId = Session["UserId"] as int?;
            if (!userId.HasValue)
            {
                return ErrorJson(401, "NotAuthenticated", "You need to log in to do this.");
            }

            bool malformed;
            var body = RequestBody.Read<RegisterPackageRequest>(Request, out malformed);
            if (malformed)
            {
                return ErrorJson(400, "ValidationError", "The request body is not valid JSON.");
            }

            if (body == null)
            {
                return ErrorJson(400, "ValidationError", "The package details are required.");
            }

            var recipient = body.Recipient ?? new RegisterPackageRequest.RecipientDetails();

            var result = Registration.Register(new PackageRegistrationRequest
            {
                RecipientFullName = recipient.FullName,
                RecipientIdentifierNo = recipient.IdentifierNo,
                RecipientEmail = recipient.Email,
                RecipientPhone = recipient.Phone,
                RecipientDepartment = recipient.Department,
                SenderName = body.SenderName,
                PackageType = body.PackageType,
                Classification = body.Classification,
                StorageLocationId = body.StorageLocationId,
                Notes = body.Notes
            }, userId.Value);

            if (!result.Success)
            {
                return ErrorJson(400, result.ErrorCode, result.Message);
            }

            Response.StatusCode = 201;
            return Json(new
            {
                packageId = result.PackageId,
                f20Identifier = result.F20Identifier,
                fee = result.Fee,
                paymentStatus = result.PaymentStatus,
                status = "Registered"
            });
        }

        /// <summary>GET /api/packages/{f20Identifier}/qr: the label's QR code as a PNG. The code holds only the identifier (CON-008).</summary>
        [RoleAuthorize]
        [HttpGet]
        [Route("api/packages/{f20Identifier}/qr")]
        public ActionResult QrCode(string f20Identifier)
        {
            // Only draw codes for packages that exist, so this can't be used as a QR maker for any text
            string identifier;
            var package = PackageIdentifier.TryNormalize(f20Identifier, out identifier)
                ? _packages.GetByF20Identifier(identifier)
                : null;

            if (package == null)
            {
                return ErrorJson(404, "NotFound", "No package was found for that code.");
            }

            // The image never changes, but it is behind a login, so only the browser may keep a copy
            Response.Cache.SetCacheability(HttpCacheability.Private);
            return File(QrCodeImage.Png(package.F20Identifier), "image/png");
        }
    }
}