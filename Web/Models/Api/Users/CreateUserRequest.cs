namespace CourierService.Web.Models.Api.Users
{
    /// <summary>Body of POST /api/users (T43).</summary>
    public class CreateUserRequest
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }
    }
}