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
