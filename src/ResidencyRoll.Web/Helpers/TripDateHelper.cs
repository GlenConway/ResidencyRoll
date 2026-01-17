using System;

namespace ResidencyRoll.Web.Helpers;

public static class TripDateHelper
{
    /// <summary>
    /// For new trips, keep arrival date in sync with departure date to reduce double entry.
    /// Existing trips are left untouched.
    /// </summary>
    public static DateTime? GetSyncedArrivalDate(DateTime? departureDate, DateTime? currentArrivalDate, bool isExistingTrip)
    {
        if (!departureDate.HasValue)
            return currentArrivalDate;

        if (isExistingTrip)
            return currentArrivalDate;

        return departureDate.Value.Date;
    }
}
