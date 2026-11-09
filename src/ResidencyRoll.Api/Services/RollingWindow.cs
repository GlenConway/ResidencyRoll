namespace ResidencyRoll.Api.Services;

/// <summary>
/// A rolling residency window is 365 days when both the start and the end count.
/// The start is 364 days before the end.
/// </summary>
public static class RollingWindow
{
    public const int LengthDays = 365;

    public static DateOnly StartOn(DateOnly end) => end.AddDays(-(LengthDays - 1));

    public static DateTime StartOn(DateTime end) => end.Date.AddDays(-(LengthDays - 1));
}
