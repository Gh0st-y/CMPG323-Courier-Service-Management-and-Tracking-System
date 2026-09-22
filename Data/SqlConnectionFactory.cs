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
            connection.Open();
            return connection;
        }
    }
}
