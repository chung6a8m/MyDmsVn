using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace MyDmsVn.Contracts
{
    public sealed class ApiError
    {
        public ApiError(
            ApiStatusCode status,
            string code,
            string message,
            IEnumerable<ApiErrorDetail>? details = null,
            string? correlationId = null)
        {
            Status = status;
            Code = RequireText(code, nameof(code));
            Message = RequireText(message, nameof(message));
            Details = (details ?? Array.Empty<ApiErrorDetail>()).ToArray();
            CorrelationId = NormalizeCorrelationId(correlationId);
        }

        [JsonIgnore]
        public ApiStatusCode Status { get; }

        public string Code { get; }

        public string Message { get; }

        public IReadOnlyList<ApiErrorDetail> Details { get; }

        public string? CorrelationId { get; }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty value is required.", parameterName);
            }

            return value;
        }

        private static string? NormalizeCorrelationId(string? correlationId)
        {
            if (correlationId == null)
            {
                return null;
            }

            var normalized = correlationId.Trim();
            if (normalized.Length == 0 || normalized.Length > 128 ||
                normalized.Any(character =>
                    !char.IsLetterOrDigit(character) && character != '-' && character != '_'))
            {
                throw new ArgumentException(
                    "The correlation ID contains unsupported characters.",
                    nameof(correlationId));
            }

            return normalized;
        }
    }
}
