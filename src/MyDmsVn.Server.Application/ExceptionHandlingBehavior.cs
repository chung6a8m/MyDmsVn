using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;

namespace MyDmsVn.Server.Application
{
    public sealed class ExceptionHandlingBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull, IRequest<TResponse>, IApplicationErrorResponse<TResponse>
    {
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            try
            {
                return await next().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                return request.FromErrors(
                    new[]
                    {
                        Error.Unexpected(
                            "InternalError",
                            "An unexpected error occurred."),
                    });
            }
        }
    }
}
