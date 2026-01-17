using ResidencyRoll.Api.Models;
using ResidencyRoll.Shared.Trips;

namespace ResidencyRoll.Api.Mappings;

internal static class TripMapping
{
    public static TripDto ToDto(this Trip trip) => new()
    {
        Id = trip.Id,
        DepartureCountry = trip.DepartureCountry,
        DepartureCity = trip.DepartureCity,
        DepartureDateTime = trip.DepartureDateTime,
        DepartureTimezone = trip.DepartureTimezone,
        ArrivalCountry = trip.ArrivalCountry,
        ArrivalCity = trip.ArrivalCity,
        ArrivalDateTime = trip.ArrivalDateTime,
        ArrivalTimezone = trip.ArrivalTimezone
    };

    public static Trip ToEntity(this TripDto dto) => new()
    {
        Id = dto.Id,
        DepartureCountry = dto.DepartureCountry,
        DepartureCity = dto.DepartureCity,
        DepartureDateTime = dto.DepartureDateTime,
        DepartureTimezone = dto.DepartureTimezone,
        ArrivalCountry = dto.ArrivalCountry,
        ArrivalCity = dto.ArrivalCity,
        ArrivalDateTime = dto.ArrivalDateTime,
        ArrivalTimezone = dto.ArrivalTimezone,
        // Legacy fields - computed properties will handle these
        CountryName = dto.ArrivalCountry,
        StartDate = dto.ArrivalDateTime.Date,
        EndDate = dto.DepartureDateTime.Date
    };
}

