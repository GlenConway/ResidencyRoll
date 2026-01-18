using System;
using ResidencyRoll.Shared.Trips;

namespace ResidencyRoll.Web.Helpers;

public static class TripLegFactory
{
    public static TripDto CreateNextLeg(TripDto baseTrip, TimeSpan? layover = null, TimeSpan? defaultDuration = null)
    {
        var lay = layover ?? TimeSpan.FromHours(2);
        var duration = defaultDuration ?? TimeSpan.FromHours(2);

        var baseArrival = baseTrip.ArrivalDateTime == default ? DateTime.UtcNow : baseTrip.ArrivalDateTime;
        var departureTime = baseArrival.Add(lay);
        var tz = string.IsNullOrWhiteSpace(baseTrip.ArrivalTimezone) ? "UTC" : baseTrip.ArrivalTimezone;

        return new TripDto
        {
            DepartureCountry = baseTrip.ArrivalCountry,
            DepartureCity = baseTrip.ArrivalCity,
            DepartureTimezone = tz,
            DepartureIataCode = baseTrip.ArrivalIataCode,
            DepartureDateTime = departureTime,
            ArrivalTimezone = tz,
            ArrivalDateTime = departureTime.Add(duration),
            ArrivalCountry = string.Empty,
            ArrivalCity = string.Empty,
            ArrivalIataCode = null
        };
    }
}
