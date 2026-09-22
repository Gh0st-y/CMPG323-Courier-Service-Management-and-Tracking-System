using System.Data;

namespace CourierService.Data
{
    /// <summary>
    /// Small helper so every repository builds parameters the same way. Always use this (or
    /// the equivalent) instead of concatenating values into command text — every query in this
    /// project must be parameterised, no exceptions (CLAUDE.md / SR-03).
    /// </summary>
    public static class DbCommandExtensions
    {
        public static void AddParameter(this IDbCommand command, string name, DbType type, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = type;
            parameter.Value = value ?? System.DBNull.Value;
            command.Parameters.Add(parameter);
        }

        public static void SetCommand(this IDbCommand command, IDbConnection connection, IDbTransaction transaction, string commandText)
        {
            command.Connection = connection;
            command.Transaction = transaction;
            command.CommandText = commandText;
        }
    }
}
