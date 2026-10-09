using System.Collections.Generic;
using System.Linq;
using ErrorOr;
using MediatR;
using MyDmsVn.Server.Application.Security;

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

    public abstract class AuthorizedActorApplicationRequest<TValue>
        : ApplicationRequest<TValue>, IAuthorizedActorRequest
    {
        private int? _authorizedUserId;

        public abstract string PermissionKey { get; }

        internal int AuthorizedUserId => _authorizedUserId ??
            throw new System.InvalidOperationException(
                "The authorized actor was not bound by the authorization pipeline.");

        void IAuthorizedActorRequest.BindAuthorizedUser(int userId)
        {
            if (userId <= 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(userId));
            }

            _authorizedUserId = userId;
        }
    }
}
