using MyDmsVn.Contracts;

namespace MyDmsVn.Server.Application
{
    public sealed class GetFoundationStatusQuery : ApplicationRequest<FoundationStatus>
    {
        public GetFoundationStatusQuery(string clientName)
        {
            ClientName = clientName;
        }

        public string ClientName { get; }
    }
}
