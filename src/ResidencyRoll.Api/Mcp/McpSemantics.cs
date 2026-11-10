namespace ResidencyRoll.Api.Mcp;

internal static class McpSemantics
{
    public const string Rules =
        "locationAtMidnight for a date is the country at 00:00 at the start of that date. A later flight the same day does not change it. Each country uses either the midnight rule (that midnight location) or the partial-day rule (any presence during the day). The ruleType in the result says which. Each country is counted over its windowType: rolling365 (365 days, both ends), taxYear (United Kingdom, 6 April to 5 April), or calendarYear.";

    public const string HomeCountry =
        " The home country is saved on the traveller. These tools use that saved value when homeCountry is omitted. Call get_home_country to read it and set_home_country to change it. Omit traveller for the signed-in person.";

    public const string Forecast =
        " Forecasts follow International Date Line crossings. totalDays is the count at windowEnd. peakDays is the highest rolling count from today, or the first hypothetical day, through windowEnd, and it can be higher than totalDays because old days roll off while new ones are added. thresholdStatus uses peakDays.";
}
