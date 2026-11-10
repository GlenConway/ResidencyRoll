using ResidencyRoll.Api.Models;

namespace ResidencyRoll.Api.Services;

/// <summary>
/// The period a country's day threshold applies to.
/// A rolling window counts both ends, so it starts 364 days before the reference date.
/// A tax year runs from its start month and day through the day before the next anniversary.
/// </summary>
public static class CountingWindow
{
    public readonly record struct Period(DateOnly Start, DateOnly End);

    public static Period For(CountryResidencyRule rule, DateOnly referenceEnd)
    {
        return rule.WindowType switch
        {
            CountingWindowType.CalendarYear => new Period(
                new DateOnly(referenceEnd.Year, 1, 1),
                new DateOnly(referenceEnd.Year, 12, 31)),
            CountingWindowType.TaxYear => TaxYear(
                referenceEnd,
                rule.TaxYearStartMonth ?? 1,
                rule.TaxYearStartDay ?? 1),
            _ => new Period(RollingWindow.StartOn(referenceEnd), referenceEnd)
        };
    }

    public static string Name(CountingWindowType windowType) => windowType switch
    {
        CountingWindowType.TaxYear => "taxYear",
        CountingWindowType.CalendarYear => "calendarYear",
        _ => "rolling365"
    };

    private static Period TaxYear(DateOnly reference, int month, int day)
    {
        var start = new DateOnly(reference.Year, month, day);
        if (reference < start)
        {
            start = start.AddYears(-1);
        }

        return new Period(start, start.AddYears(1).AddDays(-1));
    }
}
