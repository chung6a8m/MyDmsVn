using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application;
using Xunit;

namespace MyDmsVn.Server.Application.Tests
{
    public sealed class ApplicationPipelineTests
    {
        [Fact]
        public async Task Server_application_registers_and_dispatches_typed_requests()
        {
            var services = new ServiceCollection();
            services.AddServerApplication();

            using (var provider = services.BuildServiceProvider())
            {
                var sender = provider.GetRequiredService<ISender>();

                var result = await sender.Send(
                    new GetFoundationStatusQuery("Desktop"),
                    CancellationToken.None);

                Assert.False(result.IsError);
                Assert.True(result.Value.IsReady);
                Assert.Equal("Local", result.Value.Runtime);
            }
        }

        [Fact]
        public async Task Async_validation_returns_typed_errors_before_handler_runs()
        {
            var services = new ServiceCollection();
            var tracker = new HandlerInvocationTracker();
            services.AddSingleton(tracker);
            services.AddTransient<IValidator<ValidationProbeCommand>, ValidationProbeValidator>();
            services.AddTransient<
                IRequestHandler<ValidationProbeCommand, ErrorOr<string>>,
                ValidationProbeHandler>();
            services.AddServerApplication();

            using (var provider = services.BuildServiceProvider())
            {
                var sender = provider.GetRequiredService<ISender>();

                var result = await sender.Send(
                    new ValidationProbeCommand(
                        new[] { new ValidationProbeLine(0m) }),
                    CancellationToken.None);

                Assert.True(result.IsError);
                Assert.Equal(ErrorType.Validation, result.FirstError.Type);
                Assert.Equal("Validation.Positive", result.FirstError.Code);
                Assert.Equal("Lines[0].Quantity", result.FirstError.Metadata!["Field"]);
                Assert.Equal(0, tracker.Count);
            }
        }

        [Fact]
        public async Task Multiple_async_validators_use_independent_contexts_without_duplicate_failures()
        {
            var behavior = new ValidationBehavior<ValidationProbeCommand, ErrorOr<string>>(
                new IValidator<ValidationProbeCommand>[]
                {
                    new ValidationProbeValidator(),
                    new ValidationProbeCodeValidator(),
                });
            var request = new ValidationProbeCommand(
                new[] { new ValidationProbeLine(0m) });

            var result = await behavior.Handle(
                request,
                _ => Task.FromResult<ErrorOr<string>>("handled"),
                CancellationToken.None);

            Assert.True(result.IsError);
            Assert.Collection(
                result.Errors,
                error =>
                {
                    Assert.Equal("Validation.Positive", error.Code);
                    Assert.Equal("Lines[0].Quantity", error.Metadata!["Field"]);
                },
                error =>
                {
                    Assert.Equal("Validation.LineRequired", error.Code);
                    Assert.Equal("Lines[0]", error.Metadata!["Field"]);
                });
        }

        [Fact]
        public async Task Validators_sharing_a_scoped_dependency_do_not_run_concurrently()
        {
            var tracker = new ConcurrentValidationTracker();
            var behavior = new ValidationBehavior<ValidationProbeCommand, ErrorOr<string>>(
                new IValidator<ValidationProbeCommand>[]
                {
                    new ConcurrencySensitiveValidator(tracker, "Validation.First"),
                    new ConcurrencySensitiveValidator(tracker, "Validation.Second"),
                });
            var request = new ValidationProbeCommand(
                new[] { new ValidationProbeLine(1m) });

            var result = await behavior.Handle(
                request,
                _ => Task.FromResult<ErrorOr<string>>("handled"),
                CancellationToken.None);

            Assert.True(result.IsError);
            Assert.False(tracker.Overlapped);
            Assert.Equal(2, result.Errors.Count);
        }

        [Fact]
        public async Task Handler_exception_becomes_an_unexpected_typed_error()
        {
            var services = new ServiceCollection();
            var reporter = new CapturingExceptionReporter();
            services.AddSingleton<IApplicationExceptionReporter>(reporter);
            services.AddTransient<
                IRequestHandler<ThrowingProbeCommand, ErrorOr<string>>,
                ThrowingProbeHandler>();
            services.AddServerApplication();

            using (var provider = services.BuildServiceProvider())
            {
                var sender = provider.GetRequiredService<ISender>();

                var result = await sender.Send(
                    new ThrowingProbeCommand(),
                    CancellationToken.None);

                Assert.True(result.IsError);
                Assert.Equal(ErrorType.Unexpected, result.FirstError.Type);
                Assert.Equal("InternalError", result.FirstError.Code);
                Assert.DoesNotContain("secret", result.FirstError.Description);
                Assert.Same(reporter.Exception, Assert.Single(reporter.Exceptions));
                Assert.Equal(typeof(ThrowingProbeCommand), reporter.RequestType);
            }
        }

        [Fact]
        public async Task Cancellation_is_rethrown_without_exception_reporting()
        {
            var reporter = new CapturingExceptionReporter();
            var behavior = new ExceptionHandlingBehavior<ThrowingProbeCommand, ErrorOr<string>>(reporter);
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => behavior.Handle(
                        new ThrowingProbeCommand(),
                        _ => Task.FromCanceled<ErrorOr<string>>(cancellation.Token),
                        cancellation.Token));
            }

            Assert.Empty(reporter.Exceptions);
        }

        private sealed class ValidationProbeCommand : ApplicationRequest<string>
        {
            public ValidationProbeCommand(IReadOnlyList<ValidationProbeLine> lines)
            {
                Lines = lines;
            }

            public IReadOnlyList<ValidationProbeLine> Lines { get; }
        }

        private sealed class ValidationProbeLine
        {
            public ValidationProbeLine(decimal quantity)
            {
                Quantity = quantity;
            }

            public decimal Quantity { get; }
        }

        private sealed class ValidationProbeValidator : AbstractValidator<ValidationProbeCommand>
        {
            public ValidationProbeValidator()
            {
                RuleForEach(request => request.Lines).ChildRules(
                    line => line.RuleFor(item => item.Quantity)
                        .MustAsync((quantity, cancellationToken) =>
                            Task.FromResult(quantity > 0m))
                        .WithErrorCode("Validation.Positive")
                        .WithMessage("Quantity must be greater than zero."));
            }
        }

        private sealed class ValidationProbeCodeValidator : AbstractValidator<ValidationProbeCommand>
        {
            public ValidationProbeCodeValidator()
            {
                RuleForEach(request => request.Lines)
                    .MustAsync((line, cancellationToken) => Task.FromResult(line.Quantity > 0m))
                    .WithErrorCode("Validation.LineRequired")
                    .WithMessage("A valid line is required.");
            }
        }

        private sealed class ConcurrencySensitiveValidator : AbstractValidator<ValidationProbeCommand>
        {
            public ConcurrencySensitiveValidator(
                ConcurrentValidationTracker tracker,
                string errorCode)
            {
                RuleFor(request => request)
                    .MustAsync(async (request, cancellationToken) =>
                    {
                        tracker.Enter();
                        try
                        {
                            await Task.Delay(50, cancellationToken);
                            return false;
                        }
                        finally
                        {
                            tracker.Exit();
                        }
                    })
                    .WithErrorCode(errorCode)
                    .WithMessage("Validation failed.");
            }
        }

        private sealed class ConcurrentValidationTracker
        {
            private int _activeCount;

            public bool Overlapped { get; private set; }

            public void Enter()
            {
                if (Interlocked.Increment(ref _activeCount) > 1)
                {
                    Overlapped = true;
                }
            }

            public void Exit()
            {
                Interlocked.Decrement(ref _activeCount);
            }
        }

        private sealed class ValidationProbeHandler
            : IRequestHandler<ValidationProbeCommand, ErrorOr<string>>
        {
            private readonly HandlerInvocationTracker _tracker;

            public ValidationProbeHandler(HandlerInvocationTracker tracker)
            {
                _tracker = tracker;
            }

            public Task<ErrorOr<string>> Handle(
                ValidationProbeCommand request,
                CancellationToken cancellationToken)
            {
                _tracker.Count++;
                ErrorOr<string> result = "handled";
                return Task.FromResult(result);
            }
        }

        private sealed class HandlerInvocationTracker
        {
            public int Count { get; set; }
        }

        private sealed class ThrowingProbeCommand : ApplicationRequest<string>
        {
        }

        private sealed class ThrowingProbeHandler
            : IRequestHandler<ThrowingProbeCommand, ErrorOr<string>>
        {
            public Task<ErrorOr<string>> Handle(
                ThrowingProbeCommand request,
                CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("secret connection detail");
            }
        }

        private sealed class CapturingExceptionReporter : IApplicationExceptionReporter
        {
            public List<Exception> Exceptions { get; } = new List<Exception>();

            public Exception? Exception => Exceptions.Count == 0 ? null : Exceptions[0];

            public Type? RequestType { get; private set; }

            public void Report(Type requestType, Exception exception)
            {
                RequestType = requestType;
                Exceptions.Add(exception);
            }
        }
    }
}
