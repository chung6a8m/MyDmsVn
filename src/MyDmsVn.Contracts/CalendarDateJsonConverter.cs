using System;
using System.Globalization;
using Newtonsoft.Json;

namespace MyDmsVn.Contracts
{
    public sealed class CalendarDateJsonConverter : JsonConverter
    {
        private const string Format = "yyyy-MM-dd";

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DateTime);
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object? existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.String ||
                reader.Value is not string text ||
                !DateTime.TryParseExact(
                    text,
                    Format,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date))
            {
                throw new JsonSerializationException(
                    "A calendar date must use the yyyy-MM-dd format.");
            }

            return DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is not DateTime date)
            {
                throw new JsonSerializationException("A calendar date value is required.");
            }

            writer.WriteValue(date.ToString(Format, CultureInfo.InvariantCulture));
        }
    }
}
