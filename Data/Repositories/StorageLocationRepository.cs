using System.Collections.Generic;
using System.Data;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;

namespace CourierService.Data.Repositories
{
    public class StorageLocationRepository : IStorageLocationRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public StorageLocationRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IEnumerable<StorageLocation> GetAll(bool activeOnly = true)
        {
            var sql = "SELECT StorageLocationId, Code, Description, IsActive FROM dbo.StorageLocations";
            if (activeOnly)
            {
                sql += " WHERE IsActive = 1";
            }
            sql += " ORDER BY Code;";

            var results = new List<StorageLocation>();

            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(MapStorageLocation(reader));
                    }
                }
            }

            return results;
        }

        public StorageLocation GetById(int storageLocationId)
        {
            const string sql = "SELECT StorageLocationId, Code, Description, IsActive FROM dbo.StorageLocations WHERE StorageLocationId = @StorageLocationId;";

            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.AddParameter("@StorageLocationId", DbType.Int32, storageLocationId);

                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? MapStorageLocation(reader) : null;
                }
            }
        }

        public int Insert(StorageLocation location, IUnitOfWork unitOfWork = null)
        {
            const string sql = @"
                INSERT INTO dbo.StorageLocations (Code, Description, IsActive)
                VALUES (@Code, @Description, @IsActive);
                SELECT CAST(SCOPE_IDENTITY() AS int);";

            IDbConnection ownedConnection = null;
            try
            {
                IDbCommand command;
                if (unitOfWork != null)
                {
                    command = unitOfWork.Connection.CreateCommand();
                    command.Transaction = unitOfWork.Transaction;
                }
                else
                {
                    ownedConnection = _connectionFactory.CreateOpenConnection();
                    command = ownedConnection.CreateCommand();
                }

                using (command)
                {
                    command.CommandText = sql;
                    command.AddParameter("@Code", DbType.String, location.Code);
                    command.AddParameter("@Description", DbType.String, location.Description);
                    command.AddParameter("@IsActive", DbType.Boolean, location.IsActive);

                    return System.Convert.ToInt32(command.ExecuteScalar());
                }
            }
            finally
            {
                ownedConnection?.Dispose();
            }
        }

        private static StorageLocation MapStorageLocation(IDataRecord record)
        {
            var descriptionOrdinal = record.GetOrdinal("Description");

            return new StorageLocation
            {
                StorageLocationId = record.GetInt32(record.GetOrdinal("StorageLocationId")),
                Code = record.GetString(record.GetOrdinal("Code")),
                Description = record.IsDBNull(descriptionOrdinal) ? null : record.GetString(descriptionOrdinal),
                IsActive = record.GetBoolean(record.GetOrdinal("IsActive"))
            };
        }
    }
}
