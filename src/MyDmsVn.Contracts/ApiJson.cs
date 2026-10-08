using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MyDmsVn.Contracts
{
    public static class ApiJson
    {
        public static JsonSerializerSettings CreateSerializerSettings()
        {
            return new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Culture = CultureInfo.InvariantCulture,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,
            };
        }
    }
}
