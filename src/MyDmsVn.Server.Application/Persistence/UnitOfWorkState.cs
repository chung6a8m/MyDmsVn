namespace MyDmsVn.Server.Application.Persistence;

public enum UnitOfWorkState
{
    Created = 0,
    ActiveTransaction = 1,
    Committed = 2,
    RolledBack = 3,
    Disposed = 4,
}
