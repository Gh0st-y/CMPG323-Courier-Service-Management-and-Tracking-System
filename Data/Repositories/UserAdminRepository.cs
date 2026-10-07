using System;
using System.Collections.Generic;
using System.Data;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;

namespace CourierService.Data.Repositories
{
    /// User management queries (T43). Parameterised SQL only (SR-03).
    public class UserAdminRepository : IUserAdminRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public UserAdminRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IList<User> GetAll()
        {
            // No PasswordHash here: nothing that lists users should ever have it in memory
            const string sql = @"
                SELECT u.UserId, u.Username, u.Email, u.RoleId, u.IsActive, u.CreatedAtUtc, u.LastLoginUtc, ro.RoleName
                FROM dbo.Users u
                INNER JOIN dbo.Roles ro ON ro.RoleId = u.RoleId
                ORDER BY u.Username;";

            var users = new List<User>();

            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var lastLoginOrdinal = reader.GetOrdinal("LastLoginUtc");

                        users.Add(new User
                        {
                            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                            Username = reader.GetString(reader.GetOrdinal("Username")),
                            Email = reader.GetString(reader.GetOrdinal("Email")),
                            RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                            RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
                            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")),
                            LastLoginUtc = reader.IsDBNull(lastLoginOrdinal) ? (DateTime?)null : reader.GetDateTime(lastLoginOrdinal)
                        });
                    }
                }
            }

            return users;
        }

        public int? GetRoleId(string roleName)
        {
            const string sql = "SELECT RoleId FROM dbo.Roles WHERE RoleName = @RoleName;";

            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.AddParameter("@RoleName", DbType.String, roleName);

                var value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
            }
        }

        public bool EmailExists(string email)
        {
            const string sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @Email) THEN 1 ELSE 0 END;";

            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.AddParameter("@Email", DbType.String, email);

                return Convert.ToInt32(command.ExecuteScalar()) == 1;
            }
        }

        public int CountActiveUsersInRole(string roleName, IUnitOfWork unitOfWork = null)
        {
            // UPDLOCK + HOLDLOCK: inside a transaction this keeps two admins from demoting each other at the same
            // moment and leaving nobody with SystemAdmin
            const string sql = @"
                SELECT COUNT(*)
                FROM dbo.Users u WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.Roles ro ON ro.RoleId = u.RoleId
                WHERE ro.RoleName = @RoleName AND u.IsActive = 1;";

            using (var command = CreateCommand(unitOfWork, sql, out var ownedConnection))
            using (ownedConnection)
            {
                command.AddParameter("@RoleName", DbType.String, roleName);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        public void UpdateRoleAndStatus(int userId, int roleId, bool isActive, IUnitOfWork unitOfWork = null)
        {
            const string sql = "UPDATE dbo.Users SET RoleId = @RoleId, IsActive = @IsActive WHERE UserId = @UserId;";

            using (var command = CreateCommand(unitOfWork, sql, out var ownedConnection))
            using (ownedConnection)
            {
                command.AddParameter("@RoleId", DbType.Int32, roleId);
                command.AddParameter("@IsActive", DbType.Boolean, isActive);
                command.AddParameter("@UserId", DbType.Int32, userId);
                command.ExecuteNonQuery();
            }
        }

        // Same pattern as UserRepository: use the unit of work's connection when there is one,
        // otherwise open a short-lived connection that the caller disposes
        private IDbCommand CreateCommand(IUnitOfWork unitOfWork, string commandText, out IDisposable ownedConnection)
        {
            if (unitOfWork != null)
            {
                ownedConnection = null;
                var command = unitOfWork.Connection.CreateCommand();
                command.Transaction = unitOfWork.Transaction;
                command.CommandText = commandText;
                return command;
            }

            var connection = _connectionFactory.CreateOpenConnection();
            ownedConnection = connection;
            var ownedCommand = connection.CreateCommand();
            ownedCommand.CommandText = commandText;
            return ownedCommand;
        }
    }
}