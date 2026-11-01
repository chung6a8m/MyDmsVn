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
        private readonly IApplicationExceptionReporter _exceptionReporter;

        public ExceptionHandlingBehavior(IApplicationExceptionReporter exceptionReporter)
        {
            _exceptionReporter = exceptionReporter;
        }

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
            catch (Operations.WebOperationException exception)
            {
                var error = exception.Code switch
                {
                    "Auth.Unauthorized" => Error.Unauthorized(exception.Code, "Authentication is required."),
                    "Auth.Forbidden" => Error.Forbidden(exception.Code, "The current operation permission is required."),
                    "Idempotency.KeyConflict" or "Deployment.EpochMismatch" or "Operation.ResultExpired" or "Operation.ResultUnavailable"
                        => Error.Conflict(exception.Code, "The protected operation cannot execute or return its result."),
                    "Operation.Unavailable" => Error.Unexpected(exception.Code, "The operation is unavailable."),
                    _ => Error.Unexpected("InternalError", "An unexpected error occurred."),
                };
                return request.FromErrors(new[] { error });
            }
            catch (Exception exception)
            {
                _exceptionReporter.Report(typeof(TRequest), exception);
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
