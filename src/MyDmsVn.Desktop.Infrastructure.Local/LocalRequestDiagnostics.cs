using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    internal sealed class LocalRequestDiagnosticsContext
        : IRequestDiagnosticsScopeFactory, IRequestCorrelationAccessor
    {
        private readonly AsyncLocal<Scope?> _current = new AsyncLocal<Scope?>();

        public string? CorrelationId => _current.Value?.CorrelationId;

        public IRequestDiagnosticsScope Begin(string operationName)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException("An operation name is required.", nameof(operationName));
            }

            var scope = new Scope(this, _current.Value, Guid.NewGuid().ToString("N"));
            _current.Value = scope;
            return scope;
        }

        private sealed class Scope : IRequestDiagnosticsScope
        {
            private readonly LocalRequestDiagnosticsContext _owner;
            private readonly Scope? _parent;
            private bool _disposed;

            public Scope(
                LocalRequestDiagnosticsContext owner,
                Scope? parent,
                string correlationId)
            {
                _owner = owner;
                _parent = parent;
                CorrelationId = correlationId;
            }

            public string CorrelationId { get; }

            public void RecordError(string errorCode)
            {
                if (string.IsNullOrWhiteSpace(errorCode))
                {
                    throw new ArgumentException("An error code is required.", nameof(errorCode));
                }
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                if (ReferenceEquals(_owner._current.Value, this))
                {
                    _owner._current.Value = _parent;
                }
            }
        }
    }

    internal sealed class LocalRequestDispatcher
    {
        private readonly ISender _sender;
        private readonly IRequestDiagnosticsScopeFactory _scopeFactory;

        public LocalRequestDispatcher(
            ISender sender,
            IRequestDiagnosticsScopeFactory scopeFactory)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        }

        public async Task<ApiResponse<T>> SendAsync<T>(
            ApplicationRequest<T> request,
            string? requestFieldRoot,
            CancellationToken cancellationToken)
        {
            return await SendAsync(
                request,
                requestFieldRoot,
                null,
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<ApiResponse<T>> SendAsync<T>(
            ApplicationRequest<T> request,
            string? requestFieldRoot,
            Func<ApiResponse<T>, string, ApiResponse<T>>? transformResponse,
            CancellationToken cancellationToken)
        {
            using (var scope = _scopeFactory.Begin(request.GetType().Name))
            {
                try
                {
                    var result = await _sender.Send(request, cancellationToken).ConfigureAwait(false);
                    var response = ApiResponseMapper.Map(
                        result,
                        requestFieldRoot,
                        scope.CorrelationId);
                    if (transformResponse != null)
                    {
                        response = transformResponse(response, scope.CorrelationId);
                    }

                    if (!response.IsSuccess)
                    {
                        scope.RecordError(response.Error!.Code);
                    }

                    return response;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    scope.RecordError("OperationCanceled");
                    throw;
                }
            }
        }
    }

    internal sealed class LocalDesktopExceptionReporter : IDesktopExceptionReporter
    {
        private readonly IRequestDiagnosticsScopeFactory _scopeFactory;
        private readonly IApplicationExceptionReporter _exceptionReporter;

        public LocalDesktopExceptionReporter(
            IRequestDiagnosticsScopeFactory scopeFactory,
            IApplicationExceptionReporter exceptionReporter)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _exceptionReporter = exceptionReporter ??
                throw new ArgumentNullException(nameof(exceptionReporter));
        }

        public string Report(string operationName, Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            using (var scope = _scopeFactory.Begin(operationName))
            {
                scope.RecordError("Desktop.UnexpectedError");
                _exceptionReporter.Report(typeof(LocalDesktopExceptionReporter), exception);
                return scope.CorrelationId;
            }
        }
    }

    internal static class LocalRequestDiagnosticsRegistration
    {
        public static IServiceCollection AddLocalRequestDiagnostics(
            this IServiceCollection services)
        {
            services.RemoveAll<IRequestDiagnosticsScopeFactory>();
            services.RemoveAll<IRequestCorrelationAccessor>();
            services.TryAddSingleton<LocalRequestDiagnosticsContext>();
            services.AddSingleton<IRequestDiagnosticsScopeFactory>(
                provider => provider.GetRequiredService<LocalRequestDiagnosticsContext>());
            services.AddSingleton<IRequestCorrelationAccessor>(
                provider => provider.GetRequiredService<LocalRequestDiagnosticsContext>());
            services.TryAddTransient<LocalRequestDispatcher>();
            services.TryAddSingleton<IDesktopExceptionReporter, LocalDesktopExceptionReporter>();
            return services;
        }
    }
}
