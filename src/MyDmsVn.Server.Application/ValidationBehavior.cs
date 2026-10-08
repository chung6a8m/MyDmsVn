using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FluentValidation;
using MediatR;

namespace MyDmsVn.Server.Application
{
    public sealed class ValidationBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull, IRequest<TResponse>, IApplicationErrorResponse<TResponse>
    {
        private const string FieldMetadataKey = "Field";
        private readonly IReadOnlyCollection<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators.ToArray();
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (_validators.Count == 0)
            {
                return await next().ConfigureAwait(false);
            }

            var context = new ValidationContext<TRequest>(request);
            var validationTasks = _validators
                .Select(validator => validator.ValidateAsync(context, cancellationToken));
            var validationResults = await Task.WhenAll(validationTasks).ConfigureAwait(false);
            var failures = validationResults
                .SelectMany(result => result.Errors)
                .Where(failure => failure != null)
                .ToArray();

            if (failures.Length == 0)
            {
                return await next().ConfigureAwait(false);
            }

            var errors = failures
                .Select(failure => Error.Validation(
                    string.IsNullOrWhiteSpace(failure.ErrorCode)
                        ? "Validation.Error"
                        : failure.ErrorCode,
                    failure.ErrorMessage,
                    new Dictionary<string, object>
                    {
                        [FieldMetadataKey] = failure.PropertyName,
                    }))
                .ToArray();

            return request.FromErrors(errors);
        }
    }
}
