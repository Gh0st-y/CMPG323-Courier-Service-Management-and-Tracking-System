using System.Data;

namespace CourierService.Domain
{
    /// <summary>
    /// Creates ADO.NET connections to the courier database. Repositories depend on this
    /// interface, not on System.Data.SqlClient directly, so the platform can change later
    /// (IR-014) without touching callers.
    /// </summary>
    public interface IDbConnectionFactory
    {
        /// <summary>Returns a new, already-open connection. Caller owns disposal.</summary>
        IDbConnection CreateOpenConnection();
    }
}
