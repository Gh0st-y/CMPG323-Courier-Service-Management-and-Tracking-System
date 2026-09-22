using System;
using System.Data;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;

namespace CourierService.Data.Repositories
{
    public class UserRepository : IUserRepository
    {
        private const string SelectColumns = @"
            u.UserId, u.Username, u.Email, u.PasswordHash, u.RoleId, u.IsActive, u.CreatedAtUtc, u.LastLoginUtc,
            ro.RoleName";

        private readonly IDbConnectionFactory _connectionFactory;

        public UserRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public User GetByUsername(string username)
        {
            const string sql = @"
                SELECT " + SelectColumns + @"
                FROM dbo.Users u
                INNER JOIN dbo.Roles ro ON ro.RoleId = u.RoleId
                WHERE u.Username = @Username;";

            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.AddParameter("@Username", DbType.String, username);

                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? MapUser(reader) : null;
                }
            }
        }

        public User GetById(int userId)
        {
            const string sql = @"
                SELECT " + SelectColumns + @"
                FROM dbo.Users u
                INNER JOIN dbo.Roles ro ON ro.RoleId = u.RoleId
                WHERE u.UserId = @UserId;";

            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.AddParameter("@UserId", DbType.Int32, userId);

                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? MapUser(reader) : null;
                }
            }
        }

        public int Insert(User user, IUnitOfWork unitOfWork = null)
        {
            const string sql = @"
                INSERT INTO dbo.Users (Username, Email, PasswordHash, RoleId, IsActive)
                VALUES (@Username, @Email, @PasswordHash, @RoleId, @IsActive);
                SELECT CAST(SCOPE_IDENTITY() AS int);";

            using (var command = CreateCommand(unitOfWork, sql, out var ownedConnection))
            using (ownedConnection)
            {
                command.AddParameter("@Username", DbType.String, user.Username);
                command.AddParameter("@Email", DbType.String, user.Email);
                command.AddParameter("@PasswordHash", DbType.String, user.PasswordHash);
                command.AddParameter("@RoleId", DbType.Int32, user.RoleId);
                command.AddParameter("@IsActive", DbType.Boolean, user.IsActive);

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        public void UpdateLastLogin(int userId, DateTime loginTimeUtc, IUnitOfWork unitOfWork = null)
        {
            const string sql = "UPDATE dbo.Users SET LastLoginUtc = @LastLoginUtc WHERE UserId = @UserId;";

            using (var command = CreateCommand(unitOfWork, sql, out var ownedConnection))
            using (ownedConnection)
            {
                command.AddParameter("@LastLoginUtc", DbType.DateTime2, loginTimeUtc);
                command.AddParameter("@UserId", DbType.Int32, userId);
                command.ExecuteNonQuery();
            }
        }

        private static User MapUser(IDataRecord record)
        {
            var lastLoginOrdinal = record.GetOrdinal("LastLoginUtc");

            return new User
            {
                UserId = record.GetInt32(record.GetOrdinal("UserId")),
                Username = record.GetString(record.GetOrdinal("Username")),
                Email = record.GetString(record.GetOrdinal("Email")),
                PasswordHash = record.GetString(record.GetOrdinal("PasswordHash")),
                RoleId = record.GetInt32(record.GetOrdinal("RoleId")),
                RoleName = record.GetString(record.GetOrdinal("RoleName")),
                IsActive = record.GetBoolean(record.GetOrdinal("IsActive")),
                CreatedAtUtc = record.GetDateTime(record.GetOrdinal("CreatedAtUtc")),
                LastLoginUtc = record.IsDBNull(lastLoginOrdinal) ? (DateTime?)null : record.GetDateTime(lastLoginOrdinal)
            };
        }

        /// <summary>
        /// Creates a command against the shared unit-of-work connection when one is supplied,
        /// otherwise opens (and hands back, for disposal by the caller) a short-lived connection.
        /// </summary>
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
