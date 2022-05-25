﻿using System;

namespace Prowo.Web
{
    public static class DateTimeExtensions
    {
        public static DateTime ToUserTime(this DateTime dateTime)
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
            return TimeZoneInfo.ConvertTime(dateTime, timeZone);
        }
    }
}
