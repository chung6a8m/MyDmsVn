using System.Collections.Generic;
using System.Linq;
using ErrorOr;
using MediatR;

namespace MyDmsVn.Server.Application
{
    public interface IApplicationErrorResponse<TResponse>
    {
        TResponse FromErrors(IReadOnlyCollection<Error> errors);
    }

    public abstract class ApplicationRequest<TValue>
        : IRequest<ErrorOr<TValue>>, IApplicationErrorResponse<ErrorOr<TValue>>
    {
        public ErrorOr<TValue> FromErrors(IReadOnlyCollection<Error> errors)
        {
            return ErrorOrFactory.From<TValue>(errors.ToList());
        }
    }
}
