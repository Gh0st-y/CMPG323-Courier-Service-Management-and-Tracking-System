using System.IO;
using System.Web.Mvc;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Services.Auth;
using CourierService.Web.Infrastructure;
using CourierService.Web.Models.Api.Auth;
using Newtonsoft.Json;

namespace CourierService.Web.Controllers.Api
{

    /// Auth endpoints (T12, FR-01, SR-01) 

    public class AuthController : Controller
    {
        private readonly IAuthService _authService;

        public AuthController()
        {
            var connectionFactory = new SqlConnectionFactory();
            _authService = new AuthService(
                new UserRepository(connectionFactory),
                new AuditLogRepository(connectionFactory));
        }

        [HttpPost]
        [Route("api/auth/login")]
        public ActionResult Login()
        {
            var request = ReadJsonBody<LoginRequest>();

            if (request == null
                || string.IsNullOrWhiteSpace(request.Username)
                || string.IsNullOrWhiteSpace(request.Password))
            {
                return ApiError(400, "ValidationError", "Username and password are required.");
            }

            var result = _authService.Login(request.Username, request.Password);

            if (!result.Success)
            {
                return ApiError(401, "InvalidCredentials", "Invalid username or password.");
            }

            UserSession.SignIn(Session, result);

            return Json(new
            {
                userId = result.UserId,
                role = result.RoleName,
                displayName = result.Username
            });
        }

        [HttpPost]
        [Route("api/auth/logout")]
        public ActionResult Logout()
        {
            UserSession.SignOut(HttpContext);
            Response.StatusCode = 204;
            return new EmptyResult();
        }

        private T ReadJsonBody<T>() where T : class
        {
            Request.InputStream.Position = 0;

            using (var reader = new StreamReader(Request.InputStream))
            {
                var body = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(body))
                {
                    return null;
                }

                try
                {
                    return JsonConvert.DeserializeObject<T>(body);
                }
                catch (JsonException)
                {
                    return null;
                }
            }
        }

        /// Display error messages
        private ActionResult ApiError(int statusCode, string code, string message)
        {
            Response.StatusCode = statusCode;
            return Json(new { error = new { code, message } });
        }
    }
}