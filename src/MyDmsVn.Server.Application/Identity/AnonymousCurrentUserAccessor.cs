namespace MyDmsVn.Server.Application.Identity
{
    internal sealed class AnonymousCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUser Current => CurrentUser.Anonymous;
    }
}
