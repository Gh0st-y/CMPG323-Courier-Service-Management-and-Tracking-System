using System;
using System.Data;
using CourierService.Domain;
using CourierService.Domain.Entities;
using CourierService.Domain.Repositories;

namespace CourierService.Data.Repositories
{
    /// <summary>Recipient insert and F20 numbering for package registration (FR-03). Parameterised SQL only (SR-03).</summary>
    public class PackageRegistrationRepository : IPackageRegistrationRepository
    {
        public int InsertRecipient(Recipient recipient, IUnitOfWork unitOfWork)
        {
            if (unitOfWork == null) throw new ArgumentNullException(nameof(unitOfWork));

            const string sql = @"
                INSERT INTO dbo.Recipients (FullName, IdentifierNo, Email, PhoneNumber, Department)
                VALUES (@FullName, @IdentifierNo, @Email, @PhoneNumber, @Department);
                SELECT CAST(SCOPE_IDENTITY() AS int);";

            using (var command = CreateCommand(unitOfWork, sql))
            {
                command.AddParameter("@FullName", DbType.String, recipient.FullName);
                command.AddParameter("@IdentifierNo", DbType.String, recipient.IdentifierNo);
                command.AddParameter("@Email", DbType.String, recipient.Email);
                command.AddParameter("@PhoneNumber", DbType.String, recipient.PhoneNumber);
                command.AddParameter("@Department", DbType.String, recipient.Department);

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        public int NextF20Number(IUnitOfWork unitOfWork)
        {
            if (unitOfWork == null) throw new ArgumentNullException(nameof(unitOfWork));

            // Only identifiers that are "F20-" followed by digits count (TRY_CAST gives NULL for anything else, such
            // as the TEST- packages from the integration tests). UPDLOCK + HOLDLOCK hold the range until the
            // transaction ends, so a second registration waits for this one instead of getting the same number.
            const string sql = @"
                SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(F20Identifier, 5, 26) AS INT)), 0) + 1
                FROM dbo.Packages WITH (UPDLOCK, HOLDLOCK)
                WHERE F20Identifier LIKE 'F20-[0-9]%';";

            using (var command = CreateCommand(unitOfWork, sql))
            {
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private static IDbCommand CreateCommand(IUnitOfWork unitOfWork, string commandText)
        {
            var command = unitOfWork.Connection.CreateCommand();
            command.Transaction = unitOfWork.Transaction;
            command.CommandText = commandText;
            return command;
        }
    }
}