using System;

namespace CourierService.Services.Security
{
    // The role check on its own, with no web code in it, so it can be unit tested.
    // The MVC attribute in the Web project just calls this (T13, SR-02, FR-02).
    public static class RoleAccessRule
    {
        // currentRole is the role of the logged in user, or null/blank when nobody is logged in.
        // allowedRoles is the list from the API contract. An empty list means
        // "any authenticated user", like the "any authenticated" rows in the contract.
        public static AccessDecision Evaluate(string currentRole, params string[] allowedRoles)
        {
            if (string.IsNullOrWhiteSpace(currentRole))
            {
                return AccessDecision.NotAuthenticated;
            }

            if (allowedRoles == null || allowedRoles.Length == 0)
            {
                return AccessDecision.Allowed;
            }

            foreach (var allowed in allowedRoles)
            {
                if (string.Equals(allowed, currentRole, StringComparison.OrdinalIgnoreCase))
                {
                    return AccessDecision.Allowed;
                }
            }

            return AccessDecision.Forbidden;
        }
    }
}