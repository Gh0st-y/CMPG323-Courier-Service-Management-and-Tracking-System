namespace CourierService.Services.Security
{
    // Result of checking a user's role against what an endpoint allows (T13, SR-02).
    public enum AccessDecision
    {
        Allowed,
        NotAuthenticated, // no logged in user, maps to HTTP 401
        Forbidden         // logged in but wrong role, maps to HTTP 403
    }
}