namespace Taxes.Test;

static class DateTimeExtensions
{
    public static DateTime ToUtc(this (int year, int month, int day) date) =>
        new(date.year, date.month, date.day, 0, 0, 0, DateTimeKind.Utc);
}