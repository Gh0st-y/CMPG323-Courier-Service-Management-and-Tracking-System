using System;
using System.Globalization;
using System.Web.Mvc;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Services.Audit;
using CourierService.Services.Packages;
using CourierService.Services.Security;
using CourierService.Web.Infrastructure;
using CourierService.Web.Models.Api.Packages;

namespace CourierService.Web.Controllers.Api
{
    /// <summary>Recording a package's payment status (T41, FR-15, FR-17). Used by the Save button on the detail page (T42).</summary>
    public class PaymentApiController : Controller
    {
        private readonly PaymentStatusService _payments;

        public PaymentApiController()
        {
            var connectionFactory = new SqlConnectionFactory();
            _payments = new PaymentStatusService(
                new PackageRepository(connectionFactory),
                new PackagePaymentRepository(connectionFactory),
                new AuditLogger(new AuditLogRepository(connectionFactory)),
                new UnitOfWorkFactory(connectionFactory));
        }

        public PaymentApiController(PaymentStatusService payments)
        {
            _payments = payments;
        }

        /// <summary>
        /// PATCH /api/packages/{f20Identifier}/payment with { paymentStatus }: 200 { packageId, f20Identifier, paymentStatus,
        /// paymentStatusUpdatedAtUtc, paymentStatusUpdatedBy, changed }. Intake, collection staff, supervisors and admins:
        /// the fee is usually paid at collection, and storage staff don't handle money.
        /// </summary>
        [RoleAuthorize(RoleNames.IntakeClerk, RoleNames.CollectionStaff, RoleNames.Supervisor, RoleNames.SystemAdmin)]
        [HttpPatch]
        [Route("api/packages/{f20Identifier}/payment")]
        public ActionResult Update(string f20Identifier)
        {
            var userId = Session["UserId"] as int?;
            if (!userId.HasValue)
            {
                return Error(401, "NotAuthenticated", "You need to log in to do this.");
            }

            bool malformed;
            var request = RequestBody.Read<PaymentStatusRequest>(Request, out malformed);
            if (malformed)
            {
                return Error(400, "ValidationError", "The request body is not valid JSON.");
            }

            string paymentStatus;
            if (request == null || !PaymentStatuses.TryNormalize(request.PaymentStatus, out paymentStatus))
            {
                return Error(400, "ValidationError", "paymentStatus must be Paid, Unpaid or Exempt.");
            }

            string identifier;
            if (!PackageIdentifier.TryNormalize(f20Identifier, out identifier))
            {
                return Error(404, "NotFound", "No package was found for that code.");
            }

            var result = _payments.Change(identifier, paymentStatus, userId.Value);
            if (result.Outcome == PaymentChangeOutcome.PackageNotFound)
            {
                return Error(404, "NotFound", "No package was found for that code.");
            }

            var payment = result.Payment;
            return Json(new
            {
                packageId = payment.PackageId,
                f20Identifier = result.F20Identifier,
                paymentStatus = payment.PaymentStatus,
                paymentStatusUpdatedAtUtc = IsoUtc(payment.UpdatedAtUtc),
                paymentStatusUpdatedBy = payment.UpdatedBy,
                changed = result.Outcome == PaymentChangeOutcome.Changed
            });
        }

        private ActionResult Error(int statusCode, string code, string message)
        {
            Response.StatusCode = statusCode;
            Response.TrySkipIisCustomErrors = true;
            return Json(new { error = new { code, message } });
        }

        private static string IsoUtc(DateTime? value)
        {
            return value.HasValue
                ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
                : null;
        }
    }
}