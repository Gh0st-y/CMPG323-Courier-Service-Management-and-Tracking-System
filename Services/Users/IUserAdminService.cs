using System.Collections.Generic;
using CourierService.Domain.Entities;

namespace CourierService.Services.Users
{
    /// <summary>User management for System Admins (T43, FR-02, SR-01). The role check itself is on the controller.</summary>
    public interface IUserAdminService
    {
        IList<User> GetAll();

        /// <summary>Creates an active user. actingUserId is the admin doing it, for the audit log.</summary>
        UserAdminResult Create(string username, string email, string password, string role, int actingUserId);

        /// <summary>Changes the role and/or active flag. Pass null for whatever should stay the same.</summary>
        UserAdminResult Update(int userId, string role, bool? isActive, int actingUserId);
    }
}