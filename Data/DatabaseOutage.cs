using System;
using System.Data.SqlClient;
using System.Linq;
using CourierService.Domain;

namespace CourierService.Data
{
    /// <summary>
    /// Decides whether an exception means "the database is down" rather than "the code has a bug" (T52).
    /// Only the first should become a 503 Service Unavailable. A bug stays a 500 so it gets noticed.
    /// </summary>
    public static class DatabaseOutage
    {
        // SQL Server error numbers that mean the server or database can't be used at the moment:
        //    -2  command timeout           -1, 2, 53  server not found or not reachable
        //   233  connection closed by the server       942  database is offline
        //  4060  can't open the database (missing or not accessible)
        // 10053, 10054, 10060, 10061  network connection aborted, reset, timed out or refused
        // 40613  database not currently available (Azure SQL)
        private static readonly int[] OutageNumbers = { -2, -1, 2, 53, 233, 942, 4060, 10053, 10054, 10060, 10061, 40613 };

        // Severity 20 and up are fatal errors that end the connection, e.g. "A transport-level error has occurred"
        private const byte FatalSeverity = 20;

        public static bool IsOutageNumber(int sqlErrorNumber)
        {
            return OutageNumbers.Contains(sqlErrorNumber);
        }

        /// <summary>True if this exception, or any exception inside it, means the database is unavailable.</summary>
        public static bool IsOutage(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is DatabaseUnavailableException)
                {
                    return true;
                }

                var sql = current as SqlException;
                if (sql != null && (sql.Class >= FatalSeverity || sql.Errors.Cast<SqlError>().Any(e => IsOutageNumber(e.Number))))
                {
                    return true;
                }
            }

            return false;
        }
    }
}