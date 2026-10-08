using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Contracts
{
    public sealed class ApiResponseJsonConverter : JsonConverter
    {
        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType.IsGenericType &&
                objectType.GetGenericTypeDefinition() == typeof(ApiResponse<>);
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object? existingValue,
            JsonSerializer serializer)
        {
            var payload = JObject.Load(reader);
            var data = payload.GetValue("data", StringComparison.OrdinalIgnoreCase);
            var error = payload.GetValue("error", StringComparison.OrdinalIgnoreCase);

            if ((data == null) == (error == null))
            {
                throw new JsonSerializationException(
                    "An API response must contain exactly one of data or error.");
            }

            if (data != null)
            {
                if (data.Type == JTokenType.Null)
                {
                    throw new JsonSerializationException("Response data cannot be null.");
                }

                var valueType = objectType.GetGenericArguments()[0];
                var value = data.ToObject(valueType, serializer);
                return InvokeFactory(objectType, "Success", value);
            }

            if (error!.Type == JTokenType.Null)
            {
                throw new JsonSerializationException("Response error cannot be null.");
            }

            return InvokeFactory(objectType, "Failure", ReadError(error, serializer));
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }

        private static object InvokeFactory(Type responseType, string methodName, object? value)
        {
            try
            {
                return responseType
                    .GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)!
                    .Invoke(null, new[] { value })!;
            }
            catch (TargetInvocationException exception)
            {
                throw new JsonSerializationException(
                    "The API response payload is invalid.",
                    exception.InnerException ?? exception);
            }
        }

        private static ApiError ReadError(JToken token, JsonSerializer serializer)
        {
            var code = token.Value<string>("code");
            var message = token.Value<string>("message");
            var detailsToken = token["details"];
            var details = detailsToken == null || detailsToken.Type == JTokenType.Null
                ? Array.Empty<ApiErrorDetail>()
                : detailsToken.ToObject<IReadOnlyList<ApiErrorDetail>>(serializer) ??
                    Array.Empty<ApiErrorDetail>();

            try
            {
                return new ApiError(
                    ApiStatusCode.Unspecified,
                    code!,
                    message!,
                    details);
            }
            catch (ArgumentException exception)
            {
                throw new JsonSerializationException(
                    "The API error payload is invalid.",
                    exception);
            }
        }
    }
}
