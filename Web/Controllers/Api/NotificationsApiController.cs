using System;
using System.Globalization;
using System.Web.Mvc;
using CourierService.Data;
using CourierService.Services.Notifications;
using CourierService.Services.Packages;
using CourierService.Services.Security;
using CourierService.Web.Infrastructure;
using CourierService.Web.Models.Api.Packages;

namespace CourierService.Web.Controllers.Api
{
    /// <summary>Notification actions for staff (T49). Sending itself happens in the background worker (T25).</summary>
    public class NotificationsApiController : Controller
    {
        private readonly NotificationResendService _resend;

        public NotificationsApiController()
            : this(CourierServices.NotificationResend(new SqlConnectionFactory()))
        {
        }

        public NotificationsApiController(NotificationResendService resend)
        {
            _resend = resend;
        }

        /// <summary>
        /// POST /api/packages/{f20Identifier}/notifications/resend with { channel }: re-queues the package's latest
        /// notification on that channel if it failed. 200 { channel, status: "Queued", queuedAtUtc, templateKey,
        /// notificationQueueId }; 404 for an unknown package or no notification on that channel; 409 NotFailed when the
        /// latest one wasn't a failure. Supervisor and SystemAdmin only (docs/API_CONTRACT.md).
        /// </summary>
        [RoleAuthorize(RoleNames.Supervisor, RoleNames.SystemAdmin)]
        [HttpPost]
        [Route("api/packages/{f20Identifier}/notifications/resend")]
        public ActionResult Resend(string f20Identifier)
        {
            var userId = Session["UserId"] as int?;
            if (!userId.HasValue)
            {
                return Error(401, "NotAuthenticated", "You need to log in to do this.");
            }

            bool malformed;
            var request = RequestBody.Read<ResendNotificationRequest>(Request, out malformed);
            if (malformed)
            {
                return Error(400, "ValidationError", "The request body is not valid JSON.");
            }

            string channel;
            if (!NotificationResendService.TryNormalizeChannel(request == null ? null : request.Channel, out channel))
            {
                return Error(400, "ValidationError", "channel must be Email or SMS.");
            }

            string identifier;
            if (!PackageIdentifier.TryNormalize(f20Identifier, out identifier))
            {
                return Error(404, "NotFound", "No package was found for that code.");
            }

            var result = _resend.Resend(identifier, channel, userId.Value);
            switch (result.Outcome)
            {
                case ResendOutcome.Requeued:
                    return Json(new
                    {
                        channel = result.Channel,
                        status = "Queued",
                        queuedAtUtc = result.QueuedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                        templateKey = result.TemplateKey,
                        notificationQueueId = result.NotificationQueueId
                    });

                case ResendOutcome.PackageNotFound:
                    return Error(404, "NotFound", "No package was found for that code.");

                case ResendOutcome.NoNotification:
                    return Error(404, "NotFound", "This package has no " + result.Channel + " notification to resend.");

                default:
                    return Error(409, "NotFailed",
                        "The latest " + result.Channel + " notification wasn't a failure (it is " + result.CurrentStatus + "), so there is nothing to resend.");
            }
        }

        private ActionResult Error(int statusCode, string code, string message)
        {
            Response.StatusCode = statusCode;
            Response.TrySkipIisCustomErrors = true;
            return Json(new { error = new { code, message } });
        }
    }
}