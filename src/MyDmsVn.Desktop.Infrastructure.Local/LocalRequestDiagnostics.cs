using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;

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
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IRequestDiagnosticsScopeFactory _scopeFactory;
        private readonly LocalDesktopSession _session;

        public LocalRequestDispatcher(
            IServiceScopeFactory serviceScopeFactory,
            IRequestDiagnosticsScopeFactory scopeFactory,
            LocalDesktopSession session)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _session = session ?? throw new ArgumentNullException(nameof(session));
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
            => await SendCoreAsync(request, requestFieldRoot, transformResponse, false, null, cancellationToken).ConfigureAwait(false);

        public Task<ApiResponse<T>> SendForCurrentSessionAsync<T>(
            ApplicationRequest<T> request,
            string? requestFieldRoot,
            CancellationToken cancellationToken,
            long? expectedSessionVersion = null) =>
            SendCoreAsync(request, requestFieldRoot, null, true, expectedSessionVersion, cancellationToken);

        private async Task<ApiResponse<T>> SendCoreAsync<T>(
            ApplicationRequest<T> request,
            string? requestFieldRoot,
            Func<ApiResponse<T>, string, ApiResponse<T>>? transformResponse,
            bool fenceResponse,
            long? expectedSessionVersion,
            CancellationToken cancellationToken)
        {
            using (var scope = _scopeFactory.Begin(request.GetType().Name))
            using (var requestScope = _serviceScopeFactory.CreateScope())
            {
                try
                {
                    var hasActor = _session.TryCaptureActor(out var actor, out var generation);
                    // The expected owner never supplies an actor. Compare against this core's
                    // atomically captured actor/generation before any account or handler admission.
                    if (expectedSessionVersion.HasValue && (!hasActor || generation != expectedSessionVersion.Value))
                        return SessionChanged<T>(scope);
                    requestScope.ServiceProvider
                        .GetRequiredService<LocalRequestCurrentUserAccessor>()
                        .Initialize(actor);
                    if (hasActor && !_session.IsCurrent(generation))
                    {
                        var stale = ApiResponse<T>.Failure(
                            new ApiError(
                                ApiStatusCode.Unauthorized,
                                "Auth.SessionChanged",
                                "The session changed before the request was admitted.",
                                correlationId: scope.CorrelationId));
                        scope.RecordError(stale.Error!.Code);
                        return stale;
                    }

                    if (hasActor &&
                        (request is IAuthorizedRequest || request is CheckPermissionQuery))
                    {
                        bool accountIsActive;
                        try
                        {
                            accountIsActive = await requestScope.ServiceProvider
                                .GetRequiredService<IUserAccountValidator>()
                                .IsActiveAsync(actor.UserId!.Value, cancellationToken)
                                .ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception)
                        {
                            if (fenceResponse && !_session.IsCurrent(generation))
                                return SessionChanged<T>(scope);
                            var unavailable = ApiResponse<T>.Failure(
                                new ApiError(
                                    ApiStatusCode.InternalServerError,
                                    "Database.Unavailable",
                                    "Account validation is temporarily unavailable.",
                                    correlationId: scope.CorrelationId));
                            scope.RecordError(unavailable.Error!.Code);
                            return unavailable;
                        }

                        if (!accountIsActive)
                        {
                            if (fenceResponse && !_session.IsCurrent(generation))
                                return SessionChanged<T>(scope);
                            _session.RequireReauthentication(generation);
                            var inactive = ApiResponse<T>.Failure(
                                new ApiError(
                                    ApiStatusCode.Unauthorized,
                                    "Auth.Unauthorized",
                                    "The account is no longer active.",
                                    correlationId: scope.CorrelationId));
                            scope.RecordError(inactive.Error!.Code);
                            return inactive;
                        }
                    }

                    if (hasActor && !_session.IsCurrent(generation))
                    {
                        var stale = ApiResponse<T>.Failure(
                            new ApiError(
                                ApiStatusCode.Unauthorized,
                                "Auth.SessionChanged",
                                "The session changed before the request was admitted.",
                                correlationId: scope.CorrelationId));
                        scope.RecordError(stale.Error!.Code);
                        return stale;
                    }

                    var sender = requestScope.ServiceProvider.GetRequiredService<ISender>();
                    var result = await sender.Send(request, cancellationToken).ConfigureAwait(false);
                    var response = ApiResponseMapper.Map(
                        result,
                        requestFieldRoot,
                        scope.CorrelationId);
                    if (transformResponse != null)
                    {
                        response = transformResponse(response, scope.CorrelationId);
                    }

                    // Fence before expiring this request's own current Unauthorized session.
                    // A late response says nothing about whether a mutation committed.
                    if (fenceResponse && hasActor && !_session.IsCurrent(generation))
                        return SessionChanged<T>(scope);

                    if (hasActor &&
                        !response.IsSuccess &&
                        response.Error!.Status == ApiStatusCode.Unauthorized)
                    {
                        _session.RequireReauthentication(generation);
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

        private static ApiResponse<T> SessionChanged<T>(IRequestDiagnosticsScope scope)
        {
            scope.RecordError("Auth.SessionChanged");
            return ApiResponse<T>.Failure(new ApiError(ApiStatusCode.Unauthorized,
                "Auth.SessionChanged", "The session changed before the response could be published.",
                correlationId: scope.CorrelationId));
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
