using System;

namespace MyDmsVn.Contracts
{
    public sealed class FoundationStatusRequest
    {
        public FoundationStatusRequest(string clientName)
        {
            ClientName = clientName ?? throw new ArgumentNullException(nameof(clientName));
        }

        public string ClientName { get; }
    }
}
