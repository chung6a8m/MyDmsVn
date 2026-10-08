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
            IEnumerable<ApiErrorDetail>? details = null)
        {
            Status = status;
            Code = RequireText(code, nameof(code));
            Message = RequireText(message, nameof(message));
            Details = (details ?? Array.Empty<ApiErrorDetail>()).ToArray();
        }

        [JsonIgnore]
        public ApiStatusCode Status { get; }

        public string Code { get; }

        public string Message { get; }

        public IReadOnlyList<ApiErrorDetail> Details { get; }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty value is required.", parameterName);
            }

            return value;
        }
    }
}
