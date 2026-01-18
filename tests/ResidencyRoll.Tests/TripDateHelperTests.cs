using System;
using ResidencyRoll.Web.Helpers;
using Xunit;

namespace ResidencyRoll.Tests;

public class TripDateHelperTests
{
    [Fact]
    public void NewTrip_SyncsArrivalToDepartureDate()
    {
        var departure = new DateTime(2024, 7, 10, 0, 0, 0);
        var arrival = TripDateHelper.GetSyncedArrivalDate(departure, null, isExistingTrip: false);
        Assert.Equal(new DateTime(2024, 7, 10), arrival);
    }

    [Fact]
    public void ExistingTrip_DoesNotOverrideArrivalDate()
    {
        var departure = new DateTime(2024, 7, 10);
        var existingArrival = new DateTime(2024, 7, 12);
        var arrival = TripDateHelper.GetSyncedArrivalDate(departure, existingArrival, isExistingTrip: true);
        Assert.Equal(existingArrival, arrival);
    }

    [Fact]
    public void NullDeparture_DoesNotChangeArrival()
    {
        DateTime? arrival = new DateTime(2024, 7, 12);
        var result = TripDateHelper.GetSyncedArrivalDate(null, arrival, isExistingTrip: false);
        Assert.Equal(arrival, result);
    }
}
