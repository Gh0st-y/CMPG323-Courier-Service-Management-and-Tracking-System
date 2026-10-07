using System.Collections.Generic;
using CourierService.Domain.Entities;

namespace CourierService.Domain.Repositories
{
    /// <summary>
    /// The extra user queries that user management needs (T43). Kept apart from IUserRepository so the login code,
    /// and the fakes in its tests, don't have to implement things they never use.
    /// </summary>
    public interface IUserAdminRepository
    {
        /// <summary>Every user, active or not, ordered by username. PasswordHash is not loaded.</summary>
        IList<User> GetAll();

        /// <summary>The RoleId for a role name from dbo.Roles, or null if there is no such role.</summary>
        int? GetRoleId(string roleName);

        bool EmailExists(string email);

        /// <summary>
        /// How many active users have this role. Pass the unit of work so the count is taken inside the same
        /// transaction as the change it protects (the last System Admin check).
        /// </summary>
        int CountActiveUsersInRole(string roleName, IUnitOfWork unitOfWork = null);

        void UpdateRoleAndStatus(int userId, int roleId, bool isActive, IUnitOfWork unitOfWork = null);
    }
}