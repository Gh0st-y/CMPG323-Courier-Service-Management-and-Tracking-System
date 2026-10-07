namespace CourierService.Web.Models.Api.Users
{
    /// <summary>Body of PATCH /api/users/{id} (T43). Leave a field out to keep its current value.</summary>
    public class UpdateUserRequest
    {
        public string Role { get; set; }
        public bool? IsActive { get; set; }
    }
}