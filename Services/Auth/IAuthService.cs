namespace CourierService.Services.Auth
{
    public interface IAuthService
    {
        /// Verifies a username/password against DB and returns generic failure message if unsuccessful
        AuthResult Login(string username, string password);
    }
}