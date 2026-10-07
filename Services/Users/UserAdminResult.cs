using CourierService.Domain.Entities;

namespace CourierService.Services.Users
{
    public enum UserAdminOutcome
    {
        Ok,
        Created,
        ValidationError, // 400
        NotFound,        // 404
        Conflict         // 409
    }

    /// <summary>What happened to a user management request, so the controller can pick the right response.</summary>
    public class UserAdminResult
    {
        private UserAdminResult(UserAdminOutcome outcome, User user, string errorCode, string message)
        {
            Outcome = outcome;
            User = user;
            ErrorCode = errorCode;
            Message = message;
        }

        public UserAdminOutcome Outcome { get; }

        public bool Success => Outcome == UserAdminOutcome.Ok || Outcome == UserAdminOutcome.Created;

        public User User { get; }

        public string ErrorCode { get; }

        public string Message { get; }

        public static UserAdminResult Ok(User user)
        {
            return new UserAdminResult(UserAdminOutcome.Ok, user, null, null);
        }

        public static UserAdminResult Created(User user)
        {
            return new UserAdminResult(UserAdminOutcome.Created, user, null, null);
        }

        public static UserAdminResult Invalid(string message, string errorCode = "ValidationError")
        {
            return new UserAdminResult(UserAdminOutcome.ValidationError, null, errorCode, message);
        }

        public static UserAdminResult NotFound()
        {
            return new UserAdminResult(UserAdminOutcome.NotFound, null, "NotFound", "No user was found with that id.");
        }

        public static UserAdminResult Conflict(string errorCode, string message)
        {
            return new UserAdminResult(UserAdminOutcome.Conflict, null, errorCode, message);
        }
    }
}