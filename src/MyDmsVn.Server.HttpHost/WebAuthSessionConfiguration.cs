using System;
using System.Globalization;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Server.HttpHost;

public static class WebAuthSessionConfiguration
{
    public static WebAuthSessionOptions Read(Func<string, string?> readSetting)
    {
        if (readSetting == null) throw new ArgumentNullException(nameof(readSetting));
        try
        {
            int Days(string name, int fallback)
            {
                var value = readSetting(name);
                return value == null ? fallback : int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
            }
            return new WebAuthSessionOptions(Days("MYDMSVN_WEB_REFRESH_IDLE_DAYS", 7),
                Days("MYDMSVN_WEB_REFRESH_ABSOLUTE_DAYS", 30));
        }
        catch (Exception error) when (error is ArgumentException || error is FormatException || error is OverflowException)
        {
            throw new InvalidOperationException("WEB2 refresh lifetime configuration is invalid.");
        }
    }
}
