using System;
using System.Collections.Generic;

namespace MyDmsVn.Desktop.Application
{
    public sealed class CatalogEditSnapshot
    {
        public CatalogEditSnapshot(Guid editorIdentity, int? selectedId, long revision,
            bool isDirty, IReadOnlyDictionary<string, string> acceptedFields)
        {
            EditorIdentity = editorIdentity;
            SelectedId = selectedId;
            Revision = revision;
            IsDirty = isDirty;
            AcceptedFields = acceptedFields;
        }

        public Guid EditorIdentity { get; }
        public int? SelectedId { get; }
        public long Revision { get; }
        public bool IsDirty { get; }
        public IReadOnlyDictionary<string, string> AcceptedFields { get; }
    }
}
