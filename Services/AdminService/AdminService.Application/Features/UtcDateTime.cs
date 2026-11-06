namespace AdminService.Application.Features;

internal static class UtcDateTime
{
    /// <summary>Mốc thời gian nhận từ client luôn được hiểu là UTC (giá trị không ghi múi giờ coi như đã là UTC).</summary>
    public static DateTime? ToUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Unspecified } v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
        { } v => v.ToUniversalTime(),
    };
}
