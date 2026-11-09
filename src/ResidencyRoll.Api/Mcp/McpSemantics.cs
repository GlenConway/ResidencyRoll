namespace ResidencyRoll.Api.Mcp;

internal static class McpSemantics
{
    public const string Rules =
        "Each country uses either the midnight rule (the location at local midnight) or the partial-day rule (any presence during the day). The ruleType in the result says which. The usual window is the rolling 365 days ending today.";

    public const string HomeCountry =
        " The home country is saved on the user profile. These tools use that saved value when homeCountry is omitted. Call get_home_country to read it and set_home_country to change it.";

    public const string Forecast =
        " Forecasts follow International Date Line crossings. totalDays is the count at windowEnd. peakDays is the highest rolling count from today, or the first hypothetical day, through windowEnd, and it can be higher than totalDays because old days roll off while new ones are added. thresholdStatus uses peakDays.";
}
