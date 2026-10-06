using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using CourierService.Domain;

namespace CourierService.Data
{
    /// <summary>ADO.NET connection factory for SQL Server / LocalDB (DECISIONS.md #1).</summary>
    public class SqlConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        /// <summary>Reads the "CourierServiceDb" connection string from the host's config (Web.config).</summary>
        public SqlConnectionFactory()
            : this(ConfigurationManager.ConnectionStrings["CourierServiceDb"].ConnectionString)
        {
        }

        /// <summary>Explicit connection string — used by tests and tools that have no app/web config.</summary>
        public SqlConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IDbConnection CreateOpenConnection()
        {
            var connection = new SqlConnection(_connectionString);

            try
            {
                connection.Open();
                return connection;
            }
            catch (SqlException ex)
            {
                // Couldn't connect at all: server down, database offline or missing, network gone (T52)
                connection.Dispose();
                throw new DatabaseUnavailableException(ex);
            }
            catch (InvalidOperationException ex)
            {
                // Thrown when no pooled connection frees up in time, which also means the database isn't keeping up
                connection.Dispose();
                throw new DatabaseUnavailableException(ex);
            }
        }
    }
}