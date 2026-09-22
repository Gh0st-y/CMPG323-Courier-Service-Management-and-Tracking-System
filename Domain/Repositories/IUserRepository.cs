using System;
using CourierService.Domain.Entities;

namespace CourierService.Domain.Repositories
{
    public interface IUserRepository
    {
        /// <summary>Returns null if no active or inactive user matches — caller decides how to respond.</summary>
        User GetByUsername(string username);

        User GetById(int userId);

        int Insert(User user, IUnitOfWork unitOfWork = null);

        void UpdateLastLogin(int userId, DateTime loginTimeUtc, IUnitOfWork unitOfWork = null);
    }
}
