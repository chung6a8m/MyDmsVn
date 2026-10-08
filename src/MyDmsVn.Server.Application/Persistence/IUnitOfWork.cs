using System;

namespace MyDmsVn.Server.Application.Persistence;

public interface IUnitOfWork : IDisposable
{
    UnitOfWorkState State { get; }

    void BeginTransaction();

    TRepository Repository<TRepository>() where TRepository : class;

    void Commit();

    void Rollback();
}
