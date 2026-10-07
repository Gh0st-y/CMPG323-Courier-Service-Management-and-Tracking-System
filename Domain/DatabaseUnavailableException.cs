using System;

namespace CourierService.Domain
{
    /// <summary>
    /// The database can't be reached right now (server down, database offline, network gone). Thrown by the
    /// connection factory so the web layer can show "Service Unavailable" instead of a generic error (T52, NFR-022).
    /// The original error is kept as the inner exception for the log.
    /// </summary>
    public class DatabaseUnavailableException : Exception
    {
        public DatabaseUnavailableException(Exception innerException)
            : base("The database is not available.", innerException)
        {
        }
    }
}