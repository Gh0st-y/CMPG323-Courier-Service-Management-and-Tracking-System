using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Services.Audit;
using CourierService.Services.Security;
using CourierService.Services.Users;
using CourierService.Web.Infrastructure;
using CourierService.Web.Models.Api.Users;

namespace CourierService.Web.Controllers.Api
{
    /// <summary>
    /// User management API (T43, FR-02, SR-01; docs/API_CONTRACT.md). System Admin only.
    /// Called UsersApiController, not UsersController, so it can't clash with a Users page controller from the
    /// user management UI (T44). Two controllers with the same name make the default route fail.
    /// </summary>
    [RoleAuthorize(RoleNames.SystemAdmin)]
    public class UsersApiController : Controller
    {
        private readonly IUserAdminService _users;

        public UsersApiController()
            : this(new SqlConnectionFactory())
        {
        }

        private UsersApiController(IDbConnectionFactory connectionFactory)
            : this(new UserAdminService(
                new UserRepository(connectionFactory),
                new UserAdminRepository(connectionFactory),
                new AuditLogger(new AuditLogRepository(connectionFactory)),
                new UnitOfWorkFactory(connectionFactory)))
        {
        }

        public UsersApiController(IUserAdminService users)
        {
            _users = users;
        }

        /// <summary>GET /api/users: every user, active or not.</summary>
        [HttpGet]
        [Route("api/users")]
        public ActionResult List()
        {
            var users = _users.GetAll().Select(ToJson).ToList();
            return Json(users, JsonRequestBehavior.AllowGet);
        }

        /// <summary>POST /api/users: {username, email, password, role} creates an active user. 201 with the user.</summary>
        [HttpPost]
        [Route("api/users")]
        public ActionResult Create()
        {
            int actingUserId;
            if (!TryGetUserId(out actingUserId))
            {
                return Error(401, "NotAuthenticated", "You need to log in to do this.");
            }

            bool malformed;
            var request = RequestBody.Read<CreateUserRequest>(Request, out malformed);
            if (malformed)
            {
                return Error(400, "ValidationError", "The request body is not valid JSON.");
            }

            if (request == null)
            {
                return Error(400, "ValidationError", "username, email, password and role are all required.");
            }

            var result = _users.Create(request.Username, request.Email, request.Password, request.Role, actingUserId);
            if (!result.Success)
            {
                return FromFailure(result);
            }

            Response.StatusCode = 201;
            return Json(ToJson(result.User));
        }

        /// <summary>PATCH /api/users/{id}: {role?, isActive?} changes the role and/or deactivates or reactivates.</summary>
        [HttpPatch]
        [Route("api/users/{id:int}")]
        public ActionResult Update(int id)
        {
            int actingUserId;
            if (!TryGetUserId(out actingUserId))
            {
                return Error(401, "NotAuthenticated", "You need to log in to do this.");
            }

            bool malformed;
            var request = RequestBody.Read<UpdateUserRequest>(Request, out malformed);
            if (malformed)
            {
                return Error(400, "ValidationError", "The request body is not valid JSON.");
            }

            var result = _users.Update(id, request == null ? null : request.Role, request == null ? null : request.IsActive, actingUserId);
            if (!result.Success)
            {
                return FromFailure(result);
            }

            return Json(ToJson(result.User));
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            // Already answered by another filter, such as the database outage filter from T52, which runs first
            if (filterContext.ExceptionHandled)
            {
                return;
            }

            // Same as PackageActionsController: no stack traces, SQL or paths in the response (SR-03, OR-04)
            Trace.TraceError(filterContext.Exception.ToString());

            filterContext.ExceptionHandled = true;
            filterContext.HttpContext.Response.StatusCode = 500;
            filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
            filterContext.Result = new JsonResult
            {
                Data = new { error = new { code = "ServerError", message = "Something went wrong. Please try again." } },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        private bool TryGetUserId(out int userId)
        {
            var value = Session["UserId"];
            if (value is int)
            {
                userId = (int)value;
                return true;
            }

            userId = 0;
            return false;
        }

        private ActionResult FromFailure(UserAdminResult result)
        {
            switch (result.Outcome)
            {
                case UserAdminOutcome.NotFound:
                    return Error(404, result.ErrorCode, result.Message);
                case UserAdminOutcome.Conflict:
                    return Error(409, result.ErrorCode, result.Message);
                default:
                    return Error(400, result.ErrorCode, result.Message);
            }
        }

        private ActionResult Error(int statusCode, string code, string message)
        {
            Response.StatusCode = statusCode;
            Response.TrySkipIisCustomErrors = true;
            return Json(new { error = new { code, message } }, JsonRequestBehavior.AllowGet);
        }

        // The contract's fields, plus lastLoginUtc for the admin screen. Never the password hash.
        private static object ToJson(User u)
        {
            return new
            {
                userId = u.UserId,
                username = u.Username,
                email = u.Email,
                role = u.RoleName,
                isActive = u.IsActive,
                lastLoginUtc = u.LastLoginUtc.HasValue
                    ? DateTime.SpecifyKind(u.LastLoginUtc.Value, DateTimeKind.Utc)
                        .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
                    : null
            };
        }
    }
}