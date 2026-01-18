using ResidencyRoll.Api.Models;

namespace ResidencyRoll.Api.Services;

/// <summary>
/// Service for calculating residency based on location-at-midnight and partial-day rules.
/// Handles timezone conversions, International Date Line scenarios, and country-specific rules.
/// </summary>
public class ResidencyCalculationService
{
    private readonly Dictionary<string, CountryResidencyRule> _countryRules;
    
    public ResidencyCalculationService()
    {
        _countryRules = InitializeCountryRules();
    }

    /// <summary>
    /// Initialize known country residency rules.
    /// </summary>
    private Dictionary<string, CountryResidencyRule> InitializeCountryRules()
    {
        return new Dictionary<string, CountryResidencyRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["Canada"] = new CountryResidencyRule
            {
                CountryCode = "CA",
                CountryName = "Canada",
                RuleType = ResidencyRuleType.MidnightRule,
                ResidencyThresholdDays = 183
            },
            ["United Kingdom"] = new CountryResidencyRule
            {
                CountryCode = "GB",
                CountryName = "United Kingdom",
                RuleType = ResidencyRuleType.MidnightRule,
                ResidencyThresholdDays = 183
            },
            ["UK"] = new CountryResidencyRule
            {
                CountryCode = "GB",
                CountryName = "United Kingdom",
                RuleType = ResidencyRuleType.MidnightRule,
                ResidencyThresholdDays = 183
            },
            ["Australia"] = new CountryResidencyRule
            {
                CountryCode = "AU",
                CountryName = "Australia",
                RuleType = ResidencyRuleType.MidnightRule,
                ResidencyThresholdDays = 183
            },
            ["New Zealand"] = new CountryResidencyRule
            {
                CountryCode = "NZ",
                CountryName = "New Zealand",
                RuleType = ResidencyRuleType.MidnightRule,
                ResidencyThresholdDays = 183
            },
            ["United States"] = new CountryResidencyRule
            {
                CountryCode = "US",
                CountryName = "United States",
                RuleType = ResidencyRuleType.PartialDayRule,
                HasTransitException = true,
                ResidencyThresholdDays = 183
            },
            ["USA"] = new CountryResidencyRule
            {
                CountryCode = "US",
                CountryName = "United States",
                RuleType = ResidencyRuleType.PartialDayRule,
                HasTransitException = true,
                ResidencyThresholdDays = 183
            }
        };
    }

    /// <summary>
    /// Get the residency rule for a country. Defaults to Midnight Rule if not found.
    /// </summary>
    public CountryResidencyRule GetCountryRule(string countryName)
    {
        if (_countryRules.TryGetValue(countryName, out var rule))
        {
            return rule;
        }
        
        // Default to Midnight Rule for unknown countries
        return new CountryResidencyRule
        {
            CountryName = countryName,
            RuleType = ResidencyRuleType.MidnightRule,
            ResidencyThresholdDays = 183
        };
    }

    /// <summary>
    /// Generate a daily presence log for all days covered by the given trips.
    /// This is the core method that handles timezone conversions and midnight calculations.
    /// </summary>
    public List<DailyPresence> GenerateDailyPresenceLog(List<Trip> trips)
    {
        var presenceMap = new Dictionary<DateOnly, DailyPresence>();
        
        // First pass: process each trip
        foreach (var trip in trips.OrderBy(t => t.ArrivalDateTime))
        {
            ProcessTripForDailyPresence(trip, presenceMap);
        }
        
        // Second pass: fill in gaps between trips
        // Between an arrival and next departure from same location, person is stationary
        FillGapsBetweenTrips(trips, presenceMap);
        
        return presenceMap.Values.OrderBy(dp => dp.Date).ToList();
    }
    
    /// <summary>
    /// Fill in the days between arrival and departure where person stays in one location.
    /// For example: Arrive Canada July 1, depart July 4 → Fill July 2 and July 3 with Canada.
    /// </summary>
    private void FillGapsBetweenTrips(List<Trip> trips, Dictionary<DateOnly, DailyPresence> presenceMap)
    {
        // Sort trips chronologically
        var sortedTrips = trips.OrderBy(t => t.ArrivalDateTime).ToList();
        
        for (int i = 0; i < sortedTrips.Count - 1; i++)
        {
            var currentTrip = sortedTrips[i];
            var nextTrip = sortedTrips[i + 1];
            
            // Check if the next trip departs from where the current trip arrived
            if (string.Equals(currentTrip.ArrivalCountry, nextTrip.DepartureCountry, StringComparison.OrdinalIgnoreCase))
            {
                // Fill the gap between arrival and departure
                var arrivalLocal = TimeZoneInfo.ConvertTime(
                    currentTrip.ArrivalDateTime,
                    TimeZoneInfo.FindSystemTimeZoneById(currentTrip.ArrivalTimezone)
                );
                var departureLocal = TimeZoneInfo.ConvertTime(
                    nextTrip.DepartureDateTime,
                    TimeZoneInfo.FindSystemTimeZoneById(nextTrip.DepartureTimezone)
                );
                
                var arrivalDate = DateOnly.FromDateTime(arrivalLocal.Date);
                var departureDate = DateOnly.FromDateTime(departureLocal.Date);
                
                // Fill each day from arrival+1 to departure-1
                var currentDate = arrivalDate.AddDays(1);
                while (currentDate < departureDate)
                {
                    if (!presenceMap.ContainsKey(currentDate))
                    {
                        presenceMap[currentDate] = new DailyPresence
                        {
                            Date = currentDate,
                            LocationAtMidnight = currentTrip.ArrivalCountry,
                            IsInTransitAtMidnight = false,
                            LocationsDuringDay = new HashSet<string> { currentTrip.ArrivalCountry }
                        };
                    }
                    else
                    {
                        // Update if not already set
                        var presence = presenceMap[currentDate];
                        if (string.IsNullOrEmpty(presence.LocationAtMidnight))
                        {
                            presence.LocationAtMidnight = currentTrip.ArrivalCountry;
                            presence.IsInTransitAtMidnight = false;
                        }
                        presence.LocationsDuringDay.Add(currentTrip.ArrivalCountry);
                    }
                    
                    currentDate = currentDate.AddDays(1);
                }
            }
        }
    }

    /// <summary>
    /// Process a single trip and update the daily presence map.
    /// Handles timezone conversions and International Date Line scenarios.
    /// 
    /// Key insight: We check EVERY calendar date's midnight in EVERY relevant timezone
    /// to determine where the person was at that specific midnight moment.
    /// </summary>
    private void ProcessTripForDailyPresence(Trip trip, Dictionary<DateOnly, DailyPresence> presenceMap)
    {
        try
        {
            // Get timezone information
            var departureTimeZone = TimeZoneInfo.FindSystemTimeZoneById(trip.DepartureTimezone);
            var arrivalTimeZone = TimeZoneInfo.FindSystemTimeZoneById(trip.ArrivalTimezone);
            
            // Convert to UTC (the only absolute timeline)
            var departureUtc = trip.DepartureDateTime.Kind == DateTimeKind.Utc 
                ? trip.DepartureDateTime 
                : TimeZoneInfo.ConvertTimeToUtc(trip.DepartureDateTime, departureTimeZone);
            
            var arrivalUtc = trip.ArrivalDateTime.Kind == DateTimeKind.Utc 
                ? trip.ArrivalDateTime 
                : TimeZoneInfo.ConvertTimeToUtc(trip.ArrivalDateTime, arrivalTimeZone);
            
            // Get local times for reference
            var departureLocal = TimeZoneInfo.ConvertTime(departureUtc, departureTimeZone);
            var arrivalLocal = TimeZoneInfo.ConvertTime(arrivalUtc, arrivalTimeZone);
            
            // Find all calendar dates that might be relevant
            // We need to check dates in BOTH departure and arrival timezones (for IDL)
            var departureDateLocal = DateOnly.FromDateTime(departureLocal.Date);
            var arrivalDateLocal = DateOnly.FromDateTime(arrivalLocal.Date);
            
            // The relevant date range must include BOTH local dates
            var minDate = new[] { departureDateLocal, arrivalDateLocal }.Min();
            var maxDate = new[] { departureDateLocal, arrivalDateLocal }.Max();
            
            // Add buffer ONLY for eastbound IDL crossings (arrival date < departure date)
            // This handles the case where you arrive "yesterday" due to crossing the date line
            if (arrivalDateLocal < departureDateLocal)
            {
                // Eastbound IDL crossing - need extra buffer
                minDate = minDate.AddDays(-1);
                maxDate = maxDate.AddDays(1);
            }
            
            // For each calendar date in this range, check where the person was at midnight
            var currentDate = minDate;
            while (currentDate <= maxDate)
            {
                ProcessDateForTrip(currentDate, trip, departureUtc, arrivalUtc, departureTimeZone, arrivalTimeZone, presenceMap);
                currentDate = currentDate.AddDays(1);
            }
        }
        catch (TimeZoneNotFoundException ex)
        {
            // Fallback: use UTC if timezone not found
            Console.WriteLine($"Timezone not found: {ex.Message}. Using UTC as fallback.");
            ProcessTripWithUtcFallback(trip, presenceMap);
        }
    }
    
    /// <summary>
    /// For a specific calendar date, determine where the person was at midnight in various timezones.
    /// This handles the core midnight rule logic.
    /// </summary>
    private void ProcessDateForTrip(
        DateOnly date,
        Trip trip,
        DateTime departureUtc,
        DateTime arrivalUtc,
        TimeZoneInfo departureTimeZone,
        TimeZoneInfo arrivalTimeZone,
        Dictionary<DateOnly, DailyPresence> presenceMap)
    {
        // Check midnight in departure timezone for this date
        CheckMidnightInTimezone(date, trip.DepartureCountry, trip.ArrivalCountry, 
            departureUtc, arrivalUtc, departureTimeZone, presenceMap);
        
        // Also check midnight in arrival timezone (important for IDL scenarios)
        if (departureTimeZone.Id != arrivalTimeZone.Id)
        {
            CheckMidnightInTimezone(date, trip.DepartureCountry, trip.ArrivalCountry, 
                departureUtc, arrivalUtc, arrivalTimeZone, presenceMap);
        }
        
        // Track locations visited during the day (for USA Partial Day Rule)
        if (!presenceMap.TryGetValue(date, out var presence))
        {
            presence = new DailyPresence
            {
                Date = date,
                LocationAtMidnight = "",
                LocationsDuringDay = new HashSet<string>()
            };
            presenceMap[date] = presence;
        }
        
        // Check if this date overlaps with the trip at all
        // We need to determine the date range in both timezones
        var dayStartDepartureUtc = GetDayStartUtc(date, departureTimeZone);
        var dayEndDepartureUtc = GetDayEndUtc(date, departureTimeZone);
        var dayStartArrivalUtc = GetDayStartUtc(date, arrivalTimeZone);
        var dayEndArrivalUtc = GetDayEndUtc(date, arrivalTimeZone);
        
        // Add to LocationsDuringDay if physically present any part of this day in either timezone
        // Departure country: present from before departure until departure time
        if (departureUtc >= dayStartDepartureUtc && departureUtc < dayEndDepartureUtc)
        {
            presence.LocationsDuringDay.Add(trip.DepartureCountry);
        }
        
        // Arrival country: present from arrival time onward
        if (arrivalUtc >= dayStartArrivalUtc && arrivalUtc < dayEndArrivalUtc)
        {
            presence.LocationsDuringDay.Add(trip.ArrivalCountry);
        }
    }
    
    /// <summary>
    /// Check where the person was at midnight of a specific date in a specific timezone.
    /// </summary>
    private void CheckMidnightInTimezone(
        DateOnly date,
        string departureCountry,
        string arrivalCountry,
        DateTime departureUtc,
        DateTime arrivalUtc,
        TimeZoneInfo timeZone,
        Dictionary<DateOnly, DailyPresence> presenceMap)
    {
        try
        {
            // Midnight of this date in this timezone
            var midnightLocal = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified);
            var midnightUtc = TimeZoneInfo.ConvertTimeToUtc(midnightLocal, timeZone);
            
            // Determine where the person was at this exact UTC moment
            string locationAtMidnight;
            bool isInTransit;
            
            if (midnightUtc < departureUtc)
            {
                // Before departure - in departure country
                locationAtMidnight = departureCountry;
                isInTransit = false;
            }
            else if (midnightUtc >= arrivalUtc)
            {
                // After arrival - in arrival country
                locationAtMidnight = arrivalCountry;
                isInTransit = false;
            }
            else
            {
                // During flight - in transit
                locationAtMidnight = "IN_TRANSIT";
                isInTransit = true;
            }
            
            // Update or create the presence record for this date
            if (!presenceMap.TryGetValue(date, out var presence))
            {
                presence = new DailyPresence
                {
                    Date = date,
                    LocationAtMidnight = locationAtMidnight,
                    IsInTransitAtMidnight = isInTransit,
                    LocationsDuringDay = new HashSet<string>()
                };
                presenceMap[date] = presence;
            }
            else if (string.IsNullOrEmpty(presence.LocationAtMidnight) || presence.LocationAtMidnight == "")
            {
                // Only update if not yet set (first timezone checked wins)
                presence.LocationAtMidnight = locationAtMidnight;
                presence.IsInTransitAtMidnight = isInTransit;
            }
        }
        catch
        {
            // Skip invalid dates (e.g., during DST transitions)
        }
    }
    
    /// <summary>
    /// Get the UTC time for the start of a day (00:00:00) in a specific timezone.
    /// </summary>
    private DateTime GetDayStartUtc(DateOnly date, TimeZoneInfo timeZone)
    {
        try
        {
            var localStart = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }
    
    /// <summary>
    /// Get the UTC time for the end of a day (23:59:59.999) in a specific timezone.
    /// </summary>
    private DateTime GetDayEndUtc(DateOnly date, TimeZoneInfo timeZone)
    {
        try
        {
            var localEnd = new DateTime(date.Year, date.Month, date.Day, 23, 59, 59, 999, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone);
        }
        catch
        {
            return DateTime.MaxValue;
        }
    }

    /// <summary>
    /// Fallback method when timezone information is not available.
    /// </summary>
    private void ProcessTripWithUtcFallback(Trip trip, Dictionary<DateOnly, DailyPresence> presenceMap)
    {
        var departureDate = DateOnly.FromDateTime(trip.DepartureDateTime.Date);
        var arrivalDate = DateOnly.FromDateTime(trip.ArrivalDateTime.Date);
        
        AddOrUpdatePresence(presenceMap, departureDate, trip.DepartureCountry, isInTransit: false);
        
        // Mark intermediate days as in transit
        var currentDate = departureDate.AddDays(1);
        while (currentDate < arrivalDate)
        {
            AddOrUpdatePresence(presenceMap, currentDate, "IN_TRANSIT", isInTransit: true);
            currentDate = currentDate.AddDays(1);
        }
        
        AddOrUpdatePresence(presenceMap, arrivalDate, trip.ArrivalCountry, isInTransit: false);
    }

    /// <summary>
    /// Add or update a presence record for a specific date.
    /// Later trips take precedence for overlapping dates.
    /// </summary>
    private void AddOrUpdatePresence(
        Dictionary<DateOnly, DailyPresence> presenceMap,
        DateOnly date,
        string location,
        bool isInTransit)
    {
        if (!presenceMap.TryGetValue(date, out var presence))
        {
            presence = new DailyPresence
            {
                Date = date,
                LocationAtMidnight = location,
                IsInTransitAtMidnight = isInTransit,
                LocationsDuringDay = new HashSet<string>()
            };
            presenceMap[date] = presence;
        }
        
        // Later trips override earlier ones for the midnight location
        presence.LocationAtMidnight = location;
        presence.IsInTransitAtMidnight = isInTransit;
        
        // Always add to locations during the day (for partial day rule)
        if (!isInTransit && !string.IsNullOrEmpty(location))
        {
            presence.LocationsDuringDay.Add(location);
        }
    }

    /// <summary>
    /// Calculate residency days per country based on the daily presence log.
    /// Applies country-specific rules (Midnight Rule vs. Partial Day Rule).
    /// </summary>
    public Dictionary<string, int> CalculateResidencyDays(List<DailyPresence> dailyPresenceLog, DateOnly? startDate = null, DateOnly? endDate = null)
    {
        var residencyDays = new Dictionary<string, int>();
        
        // Filter by date range if provided
        var filteredLog = dailyPresenceLog;
        if (startDate.HasValue)
        {
            filteredLog = filteredLog.Where(dp => dp.Date >= startDate.Value).ToList();
        }
        if (endDate.HasValue)
        {
            filteredLog = filteredLog.Where(dp => dp.Date <= endDate.Value).ToList();
        }
        
        foreach (var presence in filteredLog)
        {
            // Skip transit days (they count for no country)
            if (presence.IsInTransitAtMidnight)
            {
                continue;
            }
            
            // Check if this country uses Partial Day Rule
            var rule = GetCountryRule(presence.LocationAtMidnight);
            
            if (rule.RuleType == ResidencyRuleType.MidnightRule)
            {
                // Midnight Rule: Count based on location at midnight
                IncrementCountryDays(residencyDays, presence.LocationAtMidnight);
            }
            else if (rule.RuleType == ResidencyRuleType.PartialDayRule)
            {
                // Partial Day Rule: Count all locations visited during the day
                foreach (var location in presence.LocationsDuringDay)
                {
                    IncrementCountryDays(residencyDays, location);
                }
            }
        }
        
        return residencyDays;
    }

    /// <summary>
    /// Calculate residency days for a specific country with special handling for USA transit rule.
    /// </summary>
    public int CalculateResidencyDaysForCountry(
        string countryName,
        List<DailyPresence> dailyPresenceLog,
        List<Trip> originalTrips,
        DateOnly? startDate = null,
        DateOnly? endDate = null)
    {
        var rule = GetCountryRule(countryName);
        var filteredLog = dailyPresenceLog.AsEnumerable();
        
        if (startDate.HasValue)
        {
            filteredLog = filteredLog.Where(dp => dp.Date >= startDate.Value);
        }
        if (endDate.HasValue)
        {
            filteredLog = filteredLog.Where(dp => dp.Date <= endDate.Value);
        }
        
        var relevantPresences = filteredLog.ToList();
        int days = 0;
        
        if (rule.RuleType == ResidencyRuleType.MidnightRule)
        {
            // Count days where location at midnight matches the country
            days = relevantPresences.Count(dp => 
                !dp.IsInTransitAtMidnight && 
                string.Equals(dp.LocationAtMidnight, countryName, StringComparison.OrdinalIgnoreCase));
        }
        else if (rule.RuleType == ResidencyRuleType.PartialDayRule)
        {
            // Count days where the country appears in locations during day
            var daysInCountry = relevantPresences
                .Where(dp => dp.LocationsDuringDay.Any(loc => 
                    string.Equals(loc, countryName, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            
            if (rule.HasTransitException)
            {
                // USA Transit Exception: Exclude days where present < 24 hours while in transit between two foreign points
                days = ApplyUsaTransitException(daysInCountry, originalTrips, countryName);
            }
            else
            {
                days = daysInCountry.Count;
            }
        }
        
        return days;
    }

    /// <summary>
    /// Apply USA transit exception: exclude days where present less than 24 hours while in transit between two foreign points.
    /// </summary>
    private int ApplyUsaTransitException(List<DailyPresence> daysInCountry, List<Trip> originalTrips, string countryName)
    {
        // For now, simplified implementation - count all days
        // TODO: Implement full transit detection logic
        // This would require analyzing trip sequences to identify:
        // 1. Trips that start and end in foreign countries
        // 2. Duration in USA is less than 24 hours
        // 3. The purpose is transit (connecting flight, etc.)
        
        return daysInCountry.Count;
    }

    private void IncrementCountryDays(Dictionary<string, int> residencyDays, string countryName)
    {
        if (!residencyDays.ContainsKey(countryName))
        {
            residencyDays[countryName] = 0;
        }
        residencyDays[countryName]++;
    }

    /// <summary>
    /// Get location at a specific timestamp based on trip history.
    /// Returns the country where the person was located at that exact moment.
    /// </summary>
    public string GetLocationAtTimestamp(DateTime utcTimestamp, List<Trip> trips)
    {
        // Find the trip that contains this timestamp
        foreach (var trip in trips.OrderBy(t => t.DepartureDateTime))
        {
            var departureUtc = trip.DepartureDateTime.ToUniversalTime();
            var arrivalUtc = trip.ArrivalDateTime.ToUniversalTime();
            
            if (utcTimestamp >= departureUtc && utcTimestamp < arrivalUtc)
            {
                // In transit
                return "IN_TRANSIT";
            }
            
            if (utcTimestamp >= arrivalUtc)
            {
                // After arrival, check if still in this country (before next departure)
                var nextTrip = trips
                    .Where(t => t.DepartureDateTime > trip.ArrivalDateTime)
                    .OrderBy(t => t.DepartureDateTime)
                    .FirstOrDefault();
                
                if (nextTrip == null || utcTimestamp < nextTrip.DepartureDateTime)
                {
                    return trip.ArrivalCountry;
                }
            }
        }
        
        // Not found in any trip
        return "UNKNOWN";
    }
}
