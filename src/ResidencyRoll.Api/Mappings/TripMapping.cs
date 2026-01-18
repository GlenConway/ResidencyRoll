using ResidencyRoll.Api.Models;
using ResidencyRoll.Shared.Trips;

namespace ResidencyRoll.Api.Mappings;

public static class TripMapping
{
    public static TripDto ToDto(this Trip trip) => new()
    {
        Id = trip.Id,
        DepartureCountry = trip.DepartureCountry,
        DepartureCity = trip.DepartureCity,
        DepartureDateTime = trip.DepartureDateTime,
        DepartureTimezone = trip.DepartureTimezone,
        DepartureIataCode = trip.DepartureIataCode,
        ArrivalCountry = trip.ArrivalCountry,
        ArrivalCity = trip.ArrivalCity,
        ArrivalDateTime = trip.ArrivalDateTime,
        ArrivalTimezone = trip.ArrivalTimezone,
        ArrivalIataCode = trip.ArrivalIataCode
    };

    public static Trip ToEntity(this TripDto dto) => new()
    {
        Id = dto.Id,
        DepartureCountry = dto.DepartureCountry,
        DepartureCity = dto.DepartureCity,
        DepartureDateTime = dto.DepartureDateTime,
        DepartureTimezone = dto.DepartureTimezone,
        DepartureIataCode = dto.DepartureIataCode,
        ArrivalCountry = dto.ArrivalCountry,
        ArrivalCity = dto.ArrivalCity,
        ArrivalDateTime = dto.ArrivalDateTime,
        ArrivalTimezone = dto.ArrivalTimezone,
        ArrivalIataCode = dto.ArrivalIataCode,
        // Legacy fields - computed properties will handle these
        CountryName = dto.ArrivalCountry,
        StartDate = dto.ArrivalDateTime,
        EndDate = dto.DepartureDateTime
    };
}

