using ResidencyRoll.Api.Models;
using ResidencyRoll.Shared.Trips;

namespace ResidencyRoll.Api.Mappings;

internal static class TripMapping
{
    public static TripDto ToDto(this Trip trip) => new()
    {
        Id = trip.Id,
        CountryName = trip.CountryName,
        StartDate = trip.StartDate,
        EndDate = trip.EndDate
    };

    public static Trip ToEntity(this TripDto dto) => new()
    {
        Id = dto.Id,
        CountryName = dto.CountryName,
        StartDate = dto.StartDate,
        EndDate = dto.EndDate
    };
}
