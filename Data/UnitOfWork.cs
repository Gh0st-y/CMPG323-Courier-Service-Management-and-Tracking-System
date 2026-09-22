using System.Data;
using CourierService.Domain;

namespace CourierService.Data
{
    /// <summary>See <see cref="IUnitOfWork"/> for usage. Rolls back on Dispose unless Commit() ran.</summary>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly IDbConnection _connection;
        private readonly IDbTransaction _transaction;
        private bool _committed;
        private bool _disposed;

        public UnitOfWork(IDbConnectionFactory connectionFactory)
        {
            _connection = connectionFactory.CreateOpenConnection();
            _transaction = _connection.BeginTransaction();
        }

        public IDbConnection Connection => _connection;

        public IDbTransaction Transaction => _transaction;

        public void Commit()
        {
            _transaction.Commit();
            _committed = true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (!_committed)
            {
                _transaction.Rollback();
            }

            _transaction.Dispose();
            _connection.Dispose();
            _disposed = true;
        }
    }

    public class UnitOfWorkFactory : IUnitOfWorkFactory
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public UnitOfWorkFactory(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IUnitOfWork Begin()
        {
            return new UnitOfWork(_connectionFactory);
        }
    }
}
