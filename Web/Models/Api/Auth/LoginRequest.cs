namespace CourierService.Web.Models.Api.Auth
{
    ///Request body for POST /api/auth/login 
    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}