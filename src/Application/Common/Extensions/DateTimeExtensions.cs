namespace Application.Common.Extensions;

public static class DateTimeExtensions
{
    // Query-string dates arrive as Kind=Unspecified; treat them as UTC instead of local time.
    public static DateTime AsUtc(this DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}