using System.Collections.Generic;
using System.Linq;
using ErrorOr;
using MediatR;

namespace MyDmsVn.Server.Application
{
    public interface IValidationFailureResponse<TResponse>
    {
        TResponse FromValidationErrors(IReadOnlyCollection<Error> errors);
    }

    public abstract class ApplicationRequest<TValue>
        : IRequest<ErrorOr<TValue>>, IValidationFailureResponse<ErrorOr<TValue>>
    {
        public ErrorOr<TValue> FromValidationErrors(IReadOnlyCollection<Error> errors)
        {
            return ErrorOrFactory.From<TValue>(errors.ToList());
        }
    }
}
