using System.Data;
using CourierService.Domain;
using CourierService.Domain.Repositories;

namespace CourierService.Data.Repositories
{
    public class AppConfigRepository : IAppConfigRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public AppConfigRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public string GetValue(string key)
        {
            const string sql = "SELECT ConfigValue FROM dbo.AppConfig WHERE ConfigKey = @ConfigKey;";

            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.AddParameter("@ConfigKey", DbType.String, key);

                var value = command.ExecuteScalar();
                return value == null || value == System.DBNull.Value ? null : (string)value;
            }
        }
    }
}