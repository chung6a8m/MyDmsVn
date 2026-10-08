using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FluentValidation;
using FluentValidation.Results;
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

            var failures = new List<ValidationFailure>();
            foreach (var validator in _validators)
            {
                var validationResult = await validator.ValidateAsync(
                    new ValidationContext<TRequest>(request),
                    cancellationToken).ConfigureAwait(false);
                failures.AddRange(
                    validationResult.Errors.Where(failure => failure != null));
            }

            if (failures.Count == 0)
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
