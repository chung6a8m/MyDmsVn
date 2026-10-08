using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MyDmsVn.Contracts
{
    public static class ApiJson
    {
        private static readonly CamelCaseNamingStrategy NamingStrategy =
            new CamelCaseNamingStrategy();

        public static JsonSerializerSettings CreateSerializerSettings()
        {
            return CreateSerializerSettings(ApiStatusCode.Unspecified);
        }

        public static ApiResponse<T> DeserializeResponse<T>(
            string json,
            ApiStatusCode responseStatus)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new System.ArgumentException("Response JSON is required.", nameof(json));
            }

            if (responseStatus == ApiStatusCode.Unspecified)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(responseStatus),
                    "The actual transport status is required.");
            }

            return JsonConvert.DeserializeObject<ApiResponse<T>>(
                json,
                CreateSerializerSettings(responseStatus))!;
        }

        private static JsonSerializerSettings CreateSerializerSettings(
            ApiStatusCode responseStatus)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Culture = CultureInfo.InvariantCulture,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,
            };
            settings.Converters.Add(new ApiResponseJsonConverter(responseStatus));
            return settings;
        }

        public static string ResolvePropertyName(string propertyName)
        {
            return NamingStrategy.GetPropertyName(propertyName, false);
        }
    }
}
