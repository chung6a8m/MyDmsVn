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
        public async Task Handler_exception_becomes_an_unexpected_typed_error()
        {
            var services = new ServiceCollection();
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
            }
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
    }
}
