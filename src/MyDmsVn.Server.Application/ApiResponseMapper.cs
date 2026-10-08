using System;
using System.Collections.Generic;
using System.Linq;
using ErrorOr;
using MyDmsVn.Contracts;

namespace MyDmsVn.Server.Application
{
    public static class ApiResponseMapper
    {
        private const string FieldMetadataKey = "Field";

        public static ApiResponse<TValue> Map<TValue>(ErrorOr<TValue> result)
        {
            if (!result.IsError)
            {
                return ApiResponse<TValue>.Success(result.Value);
            }

            var errors = result.Errors;
            if (errors.Any(error => !IsPublicErrorType(error.Type)))
            {
                return ApiResponse<TValue>.Failure(
                    new ApiError(
                        ApiStatusCode.InternalServerError,
                        "InternalError",
                        "An unexpected error occurred."));
            }

            var dominantType = GetDominantType(errors);
            var dominantError = errors
                .Where(error => error.Type == dominantType)
                .OrderBy(error => error.Code, StringComparer.Ordinal)
                .First();
            var details = IsAuthorizationErrorType(dominantType)
                ? Array.Empty<ApiErrorDetail>()
                : errors.Select(ToDetail).ToArray();

            return ApiResponse<TValue>.Failure(
                new ApiError(
                    ToStatus(dominantType),
                    ToAggregateCode(dominantType, dominantError.Code),
                    ToAggregateMessage(dominantType, dominantError.Description),
                    details));
        }

        private static bool IsPublicErrorType(ErrorType type)
        {
            return type == ErrorType.Validation ||
                type == ErrorType.Unauthorized ||
                type == ErrorType.Forbidden ||
                type == ErrorType.NotFound ||
                type == ErrorType.Conflict;
        }

        private static bool IsAuthorizationErrorType(ErrorType type)
        {
            return type == ErrorType.Unauthorized || type == ErrorType.Forbidden;
        }

        private static ErrorType GetDominantType(IReadOnlyCollection<Error> errors)
        {
            return errors
                .OrderByDescending(error => GetPrecedence(error.Type))
                .ThenBy(error => error.Code, StringComparer.Ordinal)
                .First()
                .Type;
        }

        private static int GetPrecedence(ErrorType type)
        {
            return type switch
            {
                ErrorType.Unauthorized => 5,
                ErrorType.Forbidden => 4,
                ErrorType.Conflict => 3,
                ErrorType.NotFound => 2,
                ErrorType.Validation => 1,
                _ => 6,
            };
        }

        private static ApiStatusCode ToStatus(ErrorType type)
        {
            return type switch
            {
                ErrorType.Validation => ApiStatusCode.BadRequest,
                ErrorType.Unauthorized => ApiStatusCode.Unauthorized,
                ErrorType.Forbidden => ApiStatusCode.Forbidden,
                ErrorType.NotFound => ApiStatusCode.NotFound,
                ErrorType.Conflict => ApiStatusCode.Conflict,
                _ => ApiStatusCode.InternalServerError,
            };
        }

        private static string ToAggregateCode(ErrorType type, string fallbackCode)
        {
            return type switch
            {
                ErrorType.Validation => "ValidationError",
                ErrorType.Unauthorized => "Auth.Unauthorized",
                ErrorType.Forbidden => "Auth.Forbidden",
                _ => fallbackCode,
            };
        }

        private static string ToAggregateMessage(ErrorType type, string fallbackMessage)
        {
            return type switch
            {
                ErrorType.Validation => "The request is invalid.",
                ErrorType.Unauthorized => "Authentication is required.",
                ErrorType.Forbidden => "The operation is not permitted.",
                _ => fallbackMessage,
            };
        }

        private static ApiErrorDetail ToDetail(Error error)
        {
            return new ApiErrorDetail(
                error.Code,
                error.Description,
                GetJsonFieldPath(error));
        }

        private static string? GetJsonFieldPath(Error error)
        {
            if (error.Metadata == null ||
                !error.Metadata.TryGetValue(FieldMetadataKey, out var fieldValue) ||
                fieldValue is not string field ||
                string.IsNullOrWhiteSpace(field))
            {
                return null;
            }

            return string.Join(
                ".",
                field.Split('.')
                    .Select(segment =>
                    {
                        var indexStart = segment.IndexOf('[');
                        var propertyName = indexStart < 0
                            ? segment
                            : segment.Substring(0, indexStart);
                        var suffix = indexStart < 0
                            ? string.Empty
                            : segment.Substring(indexStart);
                        return ApiJson.ResolvePropertyName(propertyName) + suffix;
                    }));
        }
    }
}
