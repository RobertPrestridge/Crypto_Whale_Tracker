namespace InvestIt.Helpers;

/// <summary>
/// Helper class for converting UTC times to US Central time for display
/// </summary>
public static class TimeZoneHelper
{
    private static readonly TimeZoneInfo CentralTimeZone;

    static TimeZoneHelper()
    {
        // Windows uses "Central Standard Time", Linux uses "America/Chicago"
        try
        {
            CentralTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            CentralTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
        }
    }

    /// <summary>
    /// Converts a UTC DateTime to US Central time
    /// </summary>
    public static DateTime ToCentralTime(DateTime utcDateTime)
    {
        if (utcDateTime.Kind == DateTimeKind.Unspecified)
        {
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, CentralTimeZone);
    }

    /// <summary>
    /// Converts a nullable UTC DateTime to US Central time
    /// </summary>
    public static DateTime? ToCentralTime(DateTime? utcDateTime)
    {
        return utcDateTime.HasValue ? ToCentralTime(utcDateTime.Value) : null;
    }

    /// <summary>
    /// Formats a UTC DateTime as a Central time string (12-hour format)
    /// </summary>
    public static string FormatCentral(DateTime utcDateTime, string format = "yyyy-MM-dd hh:mm:ss tt")
    {
        return ToCentralTime(utcDateTime).ToString(format);
    }

    /// <summary>
    /// Formats a nullable UTC DateTime as a Central time string (12-hour format)
    /// </summary>
    public static string FormatCentral(DateTime? utcDateTime, string format = "yyyy-MM-dd hh:mm:ss tt", string nullValue = "Never")
    {
        return utcDateTime.HasValue ? FormatCentral(utcDateTime.Value, format) : nullValue;
    }

    /// <summary>
    /// Gets the current date in Central time (for "today" queries)
    /// </summary>
    public static DateTime GetCentralToday()
    {
        return ToCentralTime(DateTime.UtcNow).Date;
    }

    /// <summary>
    /// Gets the start of today in UTC, based on Central time "today"
    /// </summary>
    public static DateTime GetTodayStartUtc()
    {
        var centralToday = GetCentralToday();
        return TimeZoneInfo.ConvertTimeToUtc(centralToday, CentralTimeZone);
    }

    /// <summary>
    /// Gets the end of today in UTC, based on Central time "today"
    /// </summary>
    public static DateTime GetTodayEndUtc()
    {
        var centralTomorrow = GetCentralToday().AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(centralTomorrow, CentralTimeZone);
    }
}
