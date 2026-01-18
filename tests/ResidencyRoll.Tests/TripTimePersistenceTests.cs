using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Api.Data;
using ResidencyRoll.Api.Mappings;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;
using ResidencyRoll.Shared.Trips;
using Xunit;

namespace ResidencyRoll.Tests;

public class TripTimePersistenceTests
{
    private static ApplicationDbContext BuildContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CreateTrip_PersistsDepartureAndArrivalTimes()
    {
        using var context = BuildContext(nameof(CreateTrip_PersistsDepartureAndArrivalTimes));
        var residencyService = new ResidencyCalculationService();
        var service = new TripService(context, residencyService);

        var dto = new TripDto
        {
            Id = 0,
            DepartureCountry = "Canada",
            DepartureCity = "Halifax",
            DepartureTimezone = "America/Halifax",
            DepartureDateTime = new DateTime(2025, 2, 1, 9, 45, 0),
            ArrivalCountry = "United Kingdom",
            ArrivalCity = "London",
            ArrivalTimezone = "Europe/London",
            ArrivalDateTime = new DateTime(2025, 2, 1, 12, 30, 0)
        };

        var entity = dto.ToEntity();
        entity.UserId = "user-1";

        await service.CreateTripAsync(entity);

        var saved = await service.GetTripByIdAsync(entity.Id, "user-1");
        Assert.NotNull(saved);
        Assert.Equal(dto.DepartureDateTime, saved!.DepartureDateTime);
        Assert.Equal(dto.ArrivalDateTime, saved.ArrivalDateTime);
        Assert.Equal(dto.DepartureDateTime, saved.EndDate);      // legacy field should keep time
        Assert.Equal(dto.ArrivalDateTime, saved.StartDate);      // legacy field should keep time
    }

    [Fact]
    public async Task UpdateTrip_DoesNotStripTimes()
    {
        using var context = BuildContext(nameof(UpdateTrip_DoesNotStripTimes));
        var residencyService = new ResidencyCalculationService();
        var service = new TripService(context, residencyService);

        var original = new Trip
        {
            UserId = "user-1",
            DepartureCountry = "Canada",
            DepartureCity = "Halifax",
            DepartureTimezone = "America/Halifax",
            DepartureDateTime = new DateTime(2025, 3, 10, 14, 0, 0),
            ArrivalCountry = "United Kingdom",
            ArrivalCity = "London",
            ArrivalTimezone = "Europe/London",
            ArrivalDateTime = new DateTime(2025, 3, 11, 7, 30, 0),
            CountryName = "United Kingdom",
            StartDate = new DateTime(2025, 3, 11, 7, 30, 0),
            EndDate = new DateTime(2025, 3, 10, 14, 0, 0)
        };

        await service.CreateTripAsync(original);

        // Modify times
        original.DepartureDateTime = new DateTime(2025, 3, 10, 16, 15, 0);
        original.ArrivalDateTime = new DateTime(2025, 3, 11, 9, 45, 0);
        original.StartDate = original.ArrivalDateTime;
        original.EndDate = original.DepartureDateTime;

        await service.UpdateTripAsync(original, "user-1");

        var saved = await service.GetTripByIdAsync(original.Id, "user-1");
        Assert.NotNull(saved);
        Assert.Equal(new DateTime(2025, 3, 10, 16, 15, 0), saved!.DepartureDateTime);
        Assert.Equal(new DateTime(2025, 3, 11, 9, 45, 0), saved.ArrivalDateTime);
        Assert.Equal(new DateTime(2025, 3, 11, 9, 45, 0), saved.StartDate);
        Assert.Equal(new DateTime(2025, 3, 10, 16, 15, 0), saved.EndDate);
    }
}
