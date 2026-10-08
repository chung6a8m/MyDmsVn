using ErrorOr;
using MediatR;
using MyDmsVn.Contracts;

namespace MyDmsVn.Server.Application
{
    public sealed class GetFoundationStatusQuery : IRequest<ErrorOr<FoundationStatus>>
    {
    }
}
