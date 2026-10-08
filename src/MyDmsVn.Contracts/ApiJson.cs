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
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Culture = CultureInfo.InvariantCulture,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,
            };
            settings.Converters.Add(new ApiResponseJsonConverter());
            return settings;
        }

        public static string ResolvePropertyName(string propertyName)
        {
            return NamingStrategy.GetPropertyName(propertyName, false);
        }
    }
}
