using System.Web.Mvc;
using CourierService.Data;
using CourierService.Data.Repositories;
using CourierService.Services.Auth;
using CourierService.Services.Security;
using CourierService.Web.Infrastructure;
using CourierService.Web.Models;

namespace CourierService.Web.Controllers
{
    /// <summary>
    /// The login page (T15) signing in through the real AuthService (T12): the same password check, audit entries and
    /// session keys as POST /api/auth/login, so every page and API call sees the user as logged in.
    /// </summary>
    public class AccountController : Controller
    {
        private readonly IAuthService _authService;

        public AccountController()
        {
            var connectionFactory = new SqlConnectionFactory();
            _authService = new AuthService(
                new UserRepository(connectionFactory),
                new AuditLogRepository(connectionFactory));
        }

        public AccountController(IAuthService authService)
        {
            _authService = authService;
        }

        [AllowAnonymous]
        [HttpGet]
        public ActionResult Login(string returnUrl = null)
        {
            if (UserSession.IsSignedIn(Session))
            {
                return RedirectToLocal(returnUrl);
            }

            ViewBag.ReturnUrl = returnUrl;

            if (Request.QueryString["expired"] == "1")
            {
                ViewBag.Message = "Your session has expired. Please log in again.";
            }

            return View(new LoginViewModel());
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model, string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                return LoginFailed(model, null);
            }

            var result = _authService.Login(model.UsernameOrEmail.Trim(), model.Password);
            if (!result.Success)
            {
                // The same message whatever went wrong, so the page doesn't reveal which usernames exist (SR-01)
                return LoginFailed(model, "Invalid username or password.");
            }

            UserSession.SignIn(Session, result);
            return RedirectToLocal(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            UserSession.SignOut(HttpContext);
            return RedirectToAction("Login");
        }

        private ActionResult LoginFailed(LoginViewModel model, string error)
        {
            if (error != null)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            // Never send the password back to the browser
            model.Password = null;
            ModelState.Remove(nameof(LoginViewModel.Password));
            return View(model);
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (PageAccessRule.IsLocalPath(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
    }
}