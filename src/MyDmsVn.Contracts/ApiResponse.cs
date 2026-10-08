using System;
using Newtonsoft.Json;

namespace MyDmsVn.Contracts
{
    public sealed class ApiResponse<T>
    {
        private ApiResponse(T? data, ApiError? error, bool isSuccess)
        {
            Data = data;
            Error = error;
            IsSuccess = isSuccess;
        }

        public T? Data { get; }

        public ApiError? Error { get; }

        [JsonIgnore]
        public bool IsSuccess { get; }

        public static ApiResponse<T> Success(T data)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return new ApiResponse<T>(data, null, true);
        }

        public static ApiResponse<T> Failure(ApiError error)
        {
            if (error is null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            return new ApiResponse<T>(default, error, false);
        }

        public bool ShouldSerializeData()
        {
            return IsSuccess;
        }

        public bool ShouldSerializeError()
        {
            return !IsSuccess;
        }
    }
}
