using System;

namespace MyDmsVn.Contracts
{
    public sealed class ApiErrorDetail
    {
        public ApiErrorDetail(string code, string message, string? field = null)
        {
            Code = RequireText(code, nameof(code));
            Message = RequireText(message, nameof(message));
            Field = field;
        }

        public string Code { get; }

        public string Message { get; }

        public string? Field { get; }

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
