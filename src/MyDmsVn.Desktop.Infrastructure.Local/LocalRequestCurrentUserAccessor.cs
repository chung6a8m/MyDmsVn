using System;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    internal sealed class LocalRequestCurrentUserAccessor : ICurrentUserAccessor
    {
        private CurrentUser _current = CurrentUser.Anonymous;
        private bool _initialized;

        public CurrentUser Current => _current;

        internal void Initialize(CurrentUser current)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (_initialized)
            {
                throw new InvalidOperationException("The request actor is immutable once initialized.");
            }

            _current = current;
            _initialized = true;
        }
    }
}
