using System;

namespace MyDmsVn.Desktop.Application
{
    public enum CatalogKind
    {
        Product,
        Warehouse,
        Employee,
        Customer,
    }

    public enum CatalogChangeOperation
    {
        Created,
        Updated,
        ActiveStatusChanged,
    }

    public sealed class CatalogChangedMessage
    {
        public CatalogChangedMessage(
            CatalogKind catalogKind,
            int entityId,
            CatalogChangeOperation operation)
        {
            if (entityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entityId));
            }

            CatalogKind = catalogKind;
            EntityId = entityId;
            Operation = operation;
        }

        public CatalogKind CatalogKind { get; }

        public int EntityId { get; }

        public CatalogChangeOperation Operation { get; }
    }
}
