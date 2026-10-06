namespace CourierService.Services.Auth
{
    /// Auth function
    
    public class AuthResult
    {
        public bool Success { get; private set; }
        public int UserId { get; private set; }
        public string Username { get; private set; }
        public string RoleName { get; private set; }

        public static AuthResult Fail()
        {
            return new AuthResult { Success = false };
        }

        public static AuthResult Ok(int userId, string username, string roleName)
        {
            return new AuthResult
            {
                Success = true,
                UserId = userId,
                Username = username,
                RoleName = roleName
            };
        }
    }
}