namespace Prowo.WebAsm.Server;

public static class DateTimeExtensions
{
    private static readonly TimeZoneInfo userTimeZone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    public static DateTime ToUserTime(this DateTime dateTime)
    {
        return TimeZoneInfo.ConvertTime(dateTime, TimeZoneInfo.Utc, userTimeZone);
    }

    public static DateTime FromUserTime(this DateTime dateTime)
    {
        return TimeZoneInfo.ConvertTime(dateTime, userTimeZone, TimeZoneInfo.Utc);
    }
}
