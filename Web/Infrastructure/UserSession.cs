using System;
using System.Web;
using CourierService.Services.Auth;

namespace CourierService.Web.Infrastructure
{
    /// <summary>
    /// The logged-in user, kept in the server session (T12, SR-01). The login page and the login API both sign in
    /// through here, so they fill in the same keys that [RoleAuthorize], the login check on pages and Global.asax read.
    /// </summary>
    public static class UserSession
    {
        public const string UserIdKey = "UserId";
        public const string UsernameKey = "Username";
        public const string RoleNameKey = "RoleName";

        /// <summary>ASP.NET's session cookie (Web.config sessionState has no cookieName, so it's the default).</summary>
        public const string SessionCookieName = "ASP.NET_SessionId";

        public static void SignIn(HttpSessionStateBase session, AuthResult result)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (result == null || !result.Success) throw new ArgumentException("Only a successful login can be signed in.", nameof(result));

            session[UserIdKey] = result.UserId;
            session[UsernameKey] = result.Username;
            session[RoleNameKey] = result.RoleName;
        }

        /// <summary>Ends the session and removes its cookie, so the next visit starts clean instead of looking expired.</summary>
        public static void SignOut(HttpContextBase context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            if (context.Session != null)
            {
                context.Session.Clear();
                context.Session.Abandon();
            }

            context.Response.Cookies.Add(new HttpCookie(SessionCookieName, string.Empty)
            {
                Expires = DateTime.UtcNow.AddDays(-1),
                HttpOnly = true
            });
        }

        /// <summary>Logged in means the session has a role, the same test [RoleAuthorize] uses.</summary>
        public static bool IsSignedIn(HttpSessionStateBase session)
        {
            return !string.IsNullOrWhiteSpace(RoleName(session));
        }

        public static string Username(HttpSessionStateBase session)
        {
            return session == null ? null : session[UsernameKey] as string;
        }

        public static string RoleName(HttpSessionStateBase session)
        {
            return session == null ? null : session[RoleNameKey] as string;
        }

        /// <summary>"IntakeClerk" becomes "Intake Clerk", for showing to people.</summary>
        public static string RoleDisplayName(string roleName)
        {
            if (string.IsNullOrEmpty(roleName))
            {
                return string.Empty;
            }

            var text = new System.Text.StringBuilder(roleName.Length + 4);
            for (var i = 0; i < roleName.Length; i++)
            {
                if (i > 0 && char.IsUpper(roleName[i]) && !char.IsUpper(roleName[i - 1]))
                {
                    text.Append(' ');
                }

                text.Append(roleName[i]);
            }

            return text.ToString();
        }

        /// <summary>
        /// True when the browser sent a session cookie but the server had to start a new session: the old one timed
        /// out after 30 minutes (NRF-015) or the app restarted. Used to show "Your session has expired".
        /// </summary>
        public static bool HasExpired(HttpContextBase context)
        {
            return context != null
                && context.Session != null
                && context.Session.IsNewSession
                && context.Request.Cookies[SessionCookieName] != null;
        }
    }
}