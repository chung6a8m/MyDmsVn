using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using MyDmsVn.Contracts;

namespace MyDmsVn.Server.Application
{
    internal sealed class GetFoundationStatusQueryHandler
        : IRequestHandler<GetFoundationStatusQuery, ErrorOr<FoundationStatus>>
    {
        private readonly IFoundationProbe _probe;

        public GetFoundationStatusQueryHandler(IFoundationProbe probe)
        {
            _probe = probe;
        }

        public Task<ErrorOr<FoundationStatus>> Handle(
            GetFoundationStatusQuery request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ErrorOr<FoundationStatus> result = _probe.GetStatus();
            return Task.FromResult(result);
        }
    }
}
