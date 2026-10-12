using System;
using System.Collections.Generic;
using System.Data;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;

namespace CourierService.Data.Repositories
{
    public class AuditLogQueryRepository : IAuditLogQueryRepository
    {
        // Added @SearchTerm check using wildcard matching across multiple columns
        private const string FromAndWhere = @"
            FROM dbo.AuditLog a
            LEFT JOIN dbo.Users u ON u.UserId = a.UserId
            WHERE (@SearchTerm IS NULL 
                   OR u.Username LIKE '%' + @SearchTerm + '%'
                   OR CAST(a.UserId AS NVARCHAR(50)) LIKE '%' + @SearchTerm + '%'
                   OR a.EntityType LIKE '%' + @SearchTerm + '%'
                   OR a.EntityId LIKE '%' + @SearchTerm + '%'
                   OR a.Detail LIKE '%' + @SearchTerm + '%')
              AND (@Username IS NULL OR u.Username = @Username)
              AND (@Action IS NULL OR a.Action = @Action)
              AND (@FromUtc IS NULL OR a.OccurredAtUtc >= @FromUtc)
              AND (@ToUtc IS NULL OR a.OccurredAtUtc < @ToUtc)";

        private const string CountSql = "SELECT COUNT(*) " + FromAndWhere + ";";

        private const string PageSql =
            "SELECT a.AuditLogId, a.UserId, u.Username, a.Action, a.EntityType, a.EntityId, a.Detail, a.OccurredAtUtc "
            + FromAndWhere +
            " ORDER BY a.OccurredAtUtc DESC, a.AuditLogId DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

        private readonly IDbConnectionFactory _connectionFactory;

        public AuditLogQueryRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public AuditLogPage Search(AuditLogFilter filter, int page, int pageSize)
        {
            filter = filter ?? new AuditLogFilter();

            var items = new List<AuditLogEntry>();
            int totalCount;

            using (var connection = _connectionFactory.CreateOpenConnection())
            {
                using (var countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = CountSql;
                    AddFilterParameters(countCommand, filter);
                    totalCount = Convert.ToInt32(countCommand.ExecuteScalar());
                }

                using (var pageCommand = connection.CreateCommand())
                {
                    pageCommand.CommandText = PageSql;
                    AddFilterParameters(pageCommand, filter);
                    pageCommand.AddParameter("@Offset", DbType.Int32, (page - 1) * pageSize);
                    pageCommand.AddParameter("@PageSize", DbType.Int32, pageSize);

                    using (var reader = pageCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(MapEntry(reader));
                        }
                    }
                }
            }

            return new AuditLogPage { Items = items, TotalCount = totalCount };
        }

        private static void AddFilterParameters(IDbCommand command, AuditLogFilter filter)
        {
            command.AddParameter("@SearchTerm", DbType.String, filter.SearchTerm);
            command.AddParameter("@Username", DbType.String, filter.Username);
            command.AddParameter("@Action", DbType.String, filter.Action);
            command.AddParameter("@FromUtc", DbType.DateTime2, filter.FromUtc);
            command.AddParameter("@ToUtc", DbType.DateTime2, filter.ToUtc);
        }

        private static AuditLogEntry MapEntry(IDataRecord record)
        {
            var userIdOrdinal = record.GetOrdinal("UserId");
            var usernameOrdinal = record.GetOrdinal("Username");
            var entityTypeOrdinal = record.GetOrdinal("EntityType");
            var entityIdOrdinal = record.GetOrdinal("EntityId");
            var detailOrdinal = record.GetOrdinal("Detail");

            return new AuditLogEntry
            {
                AuditLogId = record.GetInt64(record.GetOrdinal("AuditLogId")),
                UserId = record.IsDBNull(userIdOrdinal) ? (int?)null : record.GetInt32(userIdOrdinal),
                Username = record.IsDBNull(usernameOrdinal) ? null : record.GetString(usernameOrdinal),
                Action = record.GetString(record.GetOrdinal("Action")),
                EntityType = record.IsDBNull(entityTypeOrdinal) ? null : record.GetString(entityTypeOrdinal),
                EntityId = record.IsDBNull(entityIdOrdinal) ? null : record.GetString(entityIdOrdinal),
                Detail = record.IsDBNull(detailOrdinal) ? null : record.GetString(detailOrdinal),
                OccurredAtUtc = record.GetDateTime(record.GetOrdinal("OccurredAtUtc"))
            };
        }
    }
}