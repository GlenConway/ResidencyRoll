using ResidencyRoll.Api.Models;
using ResidencyRoll.Shared.Trips;

namespace ResidencyRoll.Api.Services;

/// <summary>
/// Residency reads shared by the REST controller and the MCP tools.
/// </summary>
public class ResidencyQueryService
{
    private static readonly int[] DefaultDurations = [7, 14, 30, 60, 90, 180];

    private readonly TripService _trips;
    private readonly ResidencyCalculationService _residency;

    public ResidencyQueryService(TripService trips, ResidencyCalculationService residency)
    {
        _trips = trips;
        _residency = residency;
    }

    public async Task<ForecastResponseDto> ForecastAsync(string userId, IReadOnlyList<Trip> hypotheticalTrips, DateOnly? through = null, Traveller? traveller = null)
    {
        Dictionary<string, int> current;
        Dictionary<string, int> forecast;

        if (hypotheticalTrips.Count > 0)
        {
            (current, forecast) = await _trips.ForecastDaysWithTripsAsync(userId, hypotheticalTrips.ToList(), through, traveller);
        }
        else
        {
            current = new Dictionary<string, int>();
            forecast = new Dictionary<string, int>();
        }

        return new ForecastResponseDto
        {
            Current = current.Select(kvp => new CountryDaysDto { CountryName = kvp.Key, Days = kvp.Value }).ToList(),
            Forecast = forecast.Select(kvp => new CountryDaysDto { CountryName = kvp.Key, Days = kvp.Value }).ToList()
        };
    }

    public async Task<ForecastBreakdown> ForecastBreakdownAsync(string userId, IReadOnlyList<Trip> hypotheticalTrips, DateOnly? through = null, Traveller? traveller = null)
    {
        var basic = await ForecastAsync(userId, hypotheticalTrips, through, traveller);
        var current = basic.Current.ToDictionary(row => row.CountryName, row => row.Days);
        var forecast = basic.Forecast.ToDictionary(row => row.CountryName, row => row.Days);
        if (hypotheticalTrips.Count == 0)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            return new ForecastBreakdown(RollingWindow.StartOn(today), today, current, forecast, new Dictionary<string, PeakCount>(StringComparer.OrdinalIgnoreCase));
        }

        var window = _trips.ScenarioWindow(hypotheticalTrips, through);
        var existing = await _trips.GetAllTripsAsync(userId, traveller);
        var combined = new List<Trip>(existing);
        combined.AddRange(hypotheticalTrips);
        var presence = _residency.GenerateDailyPresenceLog(combined);
        var earliest = hypotheticalTrips.Min(EarliestLocalDate);
        var todayDate = DateOnly.FromDateTime(DateTime.Today);
        var periodStart = earliest < todayDate ? earliest : todayDate;
        if (periodStart > window.End)
        {
            periodStart = window.End;
        }

        var peaks = _residency.PeakRollingCounts(presence, periodStart, window.End);
        return new ForecastBreakdown(window.Start, window.End, current, forecast, peaks);
    }

    public async Task<List<DailyPresenceDto>> GetDailyPresenceAsync(string userId, DateOnly? startDate, DateOnly? endDate, Traveller? traveller = null)
    {
        var trips = await _trips.GetAllTripsAsync(userId, traveller);
        var dailyPresence = _residency.GenerateDailyPresenceLog(trips);

        var filteredPresence = dailyPresence.AsEnumerable();
        if (startDate.HasValue)
        {
            filteredPresence = filteredPresence.Where(dp => dp.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            filteredPresence = filteredPresence.Where(dp => dp.Date <= endDate.Value);
        }

        return filteredPresence
            .OrderBy(dp => dp.Date)
            .Select(dp => new DailyPresenceDto
            {
                Date = dp.Date,
                LocationAtMidnight = dp.LocationAtMidnight,
                LocationsDuringDay = dp.LocationsDuringDay.ToList(),
                IsInTransitAtMidnight = dp.IsInTransitAtMidnight
            })
            .ToList();
    }

    public async Task<List<ResidencySummaryDto>> GetResidencySummaryAsync(string userId, DateOnly? startDate, DateOnly? endDate, Traveller? traveller = null)
    {
        var trips = await _trips.GetAllTripsAsync(userId, traveller);
        var dailyPresence = _residency.GenerateDailyPresenceLog(trips);
        if (startDate.HasValue)
        {
            var end = endDate ?? DateOnly.FromDateTime(DateTime.Today);
            var residencyDays = _residency.CalculateResidencyDays(dailyPresence, startDate, end);
            return residencyDays
                .OrderByDescending(pair => pair.Value)
                .Select(pair => SummaryRow(pair.Key, pair.Value, startDate.Value, end, "range"))
                .ToList();
        }

        var reference = endDate ?? DateOnly.FromDateTime(DateTime.Today);
        var summary = new List<ResidencySummaryDto>();
        foreach (var country in CountryNames(dailyPresence))
        {
            var rule = _residency.GetCountryRule(country);
            var period = CountingWindow.For(rule, reference);
            var counted = _residency.CalculateResidencyDays(dailyPresence, period.Start, period.End);
            if (!counted.TryGetValue(country, out var days) || days <= 0)
            {
                continue;
            }

            summary.Add(SummaryRow(country, days, period.Start, period.End, CountingWindow.Name(rule.WindowType)));
        }

        return summary.OrderByDescending(row => row.TotalDays).ToList();
    }

    public async Task<int> CountUnaccountedDaysAsync(string userId, DateOnly start, DateOnly end, Traveller? traveller = null)
    {
        var trips = await _trips.GetAllTripsAsync(userId, traveller);
        var presence = _residency.GenerateDailyPresenceLog(trips);
        return _residency.CountUnaccountedDays(presence, start, end);
    }

    public async Task<List<CalendarYearPresence>> CountCalendarYearsAsync(
        string userId,
        string country,
        IReadOnlyList<Trip> extraTrips,
        DateOnly? through,
        Traveller? traveller = null)
    {
        var dates = new List<DateOnly>();
        foreach (var trip in extraTrips)
        {
            dates.Add(DateOnly.FromDateTime(trip.DepartureDateTime));
            dates.Add(DateOnly.FromDateTime(trip.ArrivalDateTime));
        }

        if (through is DateOnly throughDate)
        {
            dates.Add(throughDate);
        }

        if (dates.Count == 0 || string.IsNullOrWhiteSpace(country))
        {
            return [];
        }

        var rule = _residency.GetCountryRule(country);
        var combined = new List<Trip>(await _trips.GetAllTripsAsync(userId, traveller));
        combined.AddRange(extraTrips);
        var presence = _residency.GenerateDailyPresenceLog(combined);
        DateOnly? lastRecorded = presence.Count == 0 ? null : presence.Max(day => day.Date);
        string? stayCountry = null;
        if (lastRecorded is DateOnly recorded)
        {
            var next = LocationFrom(combined, recorded.AddDays(1));
            if (next.Status == "projected" && !string.IsNullOrWhiteSpace(next.LocationAtMidnight))
            {
                stayCountry = _residency.GetCountryRule(next.LocationAtMidnight).CountryName;
            }
        }

        var rows = new List<CalendarYearPresence>();
        for (var year = dates.Min().Year; year <= dates.Max().Year; year++)
        {
            var start = new DateOnly(year, 1, 1);
            var windowEnd = new DateOnly(year, 12, 31);
            var days = CountCountry(_residency.CalculateResidencyDays(presence, start, windowEnd), rule.CountryName);
            DateOnly? projectedFrom = null;
            if (lastRecorded is DateOnly last && stayCountry is not null && windowEnd > last)
            {
                projectedFrom = last.AddDays(1) > start ? last.AddDays(1) : start;
                if (stayCountry.Equals(rule.CountryName, StringComparison.OrdinalIgnoreCase))
                {
                    days += windowEnd.DayNumber - projectedFrom.Value.DayNumber + 1;
                }
            }

            int? minimum = null;
            int? shortfall = null;
            string? status = null;
            if (rule.MinimumPresenceDays is int required && rule.MinimumWindowType == CountingWindowType.CalendarYear)
            {
                minimum = required;
                shortfall = Math.Max(0, required - days);
                status = shortfall == 0 ? "reached" : shortfall <= 30 ? "approaching" : "under";
            }

            rows.Add(new CalendarYearPresence(
                rule.CountryName,
                year,
                days,
                start,
                windowEnd,
                minimum,
                shortfall,
                status,
                projectedFrom,
                projectedFrom is null ? null : stayCountry));
        }

        return rows;
    }

    public async Task<RecordCoverage> GetRecordCoverageAsync(string userId, Traveller? traveller = null)
    {
        var trips = await _trips.GetAllTripsAsync(userId, traveller);
        DateOnly? first = null;
        DateOnly? last = null;
        foreach (var trip in trips)
        {
            Consider(DateOnly.FromDateTime(trip.DepartureDateTime));
            Consider(DateOnly.FromDateTime(trip.ArrivalDateTime));
        }

        return new RecordCoverage(first, last, trips.Count);

        void Consider(DateOnly date)
        {
            if (first is null || date < first)
            {
                first = date;
            }

            if (last is null || date > last)
            {
                last = date;
            }
        }
    }

    public async Task<MinimumPresence?> MeasureMinimumPresenceAsync(string userId, string country, DateOnly referenceEnd, Traveller? traveller = null)
    {
        var rule = _residency.GetCountryRule(country);
        if (rule.MinimumPresenceDays is not int minimum || rule.MinimumWindowType is not CountingWindowType windowType)
        {
            return null;
        }

        var period = CountingWindow.For(new CountryResidencyRule
        {
            WindowType = windowType,
            TaxYearStartMonth = rule.TaxYearStartMonth,
            TaxYearStartDay = rule.TaxYearStartDay
        }, referenceEnd);
        var trips = await _trips.GetAllTripsAsync(userId, traveller);
        var presence = _residency.GenerateDailyPresenceLog(trips);
        var days = CountCountry(_residency.CalculateResidencyDays(presence, period.Start, period.End), rule.CountryName);
        var shortfall = Math.Max(0, minimum - days);
        var status = shortfall == 0 ? "reached" : shortfall <= 30 ? "approaching" : "under";
        return new MinimumPresence(
            minimum,
            CountingWindow.Name(windowType),
            period.Start,
            period.End,
            days,
            shortfall,
            status);
    }

    private ResidencySummaryDto SummaryRow(string country, int days, DateOnly windowStart, DateOnly windowEnd, string windowType)
    {
        var rule = _residency.GetCountryRule(country);
        var thresholdDays = rule.ResidencyThresholdDays ?? 183;
        var daysUntilThreshold = thresholdDays - days;
        return new ResidencySummaryDto
        {
            CountryName = country,
            TotalDays = days,
            RuleType = rule.RuleType == ResidencyRuleType.MidnightRule ? "Midnight" : "PartialDay",
            ThresholdDays = thresholdDays,
            IsApproachingThreshold = daysUntilThreshold <= 30 && daysUntilThreshold > 0,
            DaysUntilThreshold = Math.Max(0, daysUntilThreshold),
            WindowType = windowType,
            WindowStart = windowStart,
            WindowEnd = windowEnd
        };
    }

    public async Task<MaxTripEndDateResponseDto> CalculateMaxEndDateAsync(
        string userId,
        MaxTripEndDateRequestDto request,
        IReadOnlyList<Trip>? additionalTrips = null,
        Traveller? traveller = null)
    {
        var limit = request.DayLimit <= 0 ? 183 : request.DayLimit;
        var existing = new List<Trip>(await _trips.GetAllTripsAsync(userId, traveller));
        if (additionalTrips is { Count: > 0 })
        {
            existing.AddRange(additionalTrips);
        }
        var bestEnd = request.TripStart;
        var best = MeasureStay(existing, request, request.TripStart);

        if (best.PeakDays > limit)
        {
            return MaxEndResponse(request, request.TripStart, best);
        }

        for (var extraDays = 1; extraDays <= limit + 366; extraDays++)
        {
            var candidate = request.TripStart.AddDays(extraDays);
            var measured = MeasureStay(existing, request, candidate);
            if (measured.PeakDays > limit)
            {
                break;
            }

            bestEnd = candidate;
            best = measured;
        }

        return MaxEndResponse(request, bestEnd, best);
    }

    private MaxTripEndDateResponseDto MaxEndResponse(MaxTripEndDateRequestDto request, DateTime end, StayMeasure measure)
    {
        var rule = _residency.GetCountryRule(request.ArrivalCountry);
        return new MaxTripEndDateResponseDto
        {
            MaxEndDate = end,
            DaysAtLimit = measure.PeakDays,
            WindowStart = measure.WindowStart,
            WindowEnd = measure.WindowEnd,
            RuleType = rule.RuleType == ResidencyRuleType.MidnightRule ? "Midnight" : "PartialDay",
            WindowType = CountingWindow.Name(rule.WindowType)
        };
    }

    public async Task<EarliestArrival> CalculateEarliestArrivalAsync(
        string userId,
        MaxTripEndDateRequestDto request,
        DateTime departure,
        IReadOnlyList<Trip>? additionalTrips = null,
        Traveller? traveller = null)
    {
        var limit = request.DayLimit <= 0 ? 183 : request.DayLimit;
        var existing = new List<Trip>(await _trips.GetAllTripsAsync(userId, traveller));
        if (additionalTrips is { Count: > 0 })
        {
            existing.AddRange(additionalTrips);
        }

        var bestArrival = departure;
        var best = MeasureStay(existing, WithStart(request, departure), departure);
        if (best.PeakDays <= limit)
        {
            for (var earlier = 1; earlier <= limit + 366; earlier++)
            {
                var candidate = departure.AddDays(-earlier);
                var measured = MeasureStay(existing, WithStart(request, candidate), departure);
                if (measured.PeakDays > limit)
                {
                    break;
                }

                bestArrival = candidate;
                best = measured;
            }
        }

        var rule = _residency.GetCountryRule(request.ArrivalCountry);
        return new EarliestArrival(
            bestArrival,
            best.PeakDays,
            best.WindowStart,
            best.WindowEnd,
            rule.RuleType == ResidencyRuleType.MidnightRule ? "Midnight" : "PartialDay",
            CountingWindow.Name(rule.WindowType));
    }

    public StayMeasure MeasureStay(IReadOnlyList<Trip> existing, MaxTripEndDateRequestDto request, DateTime end)
    {
        var hypothetical = Stay(request, end);
        var combined = new List<Trip>(existing);
        combined.AddRange(hypothetical);
        var presence = _residency.GenerateDailyPresenceLog(combined);
        var rule = _residency.GetCountryRule(request.ArrivalCountry);
        if (rule.WindowType != CountingWindowType.Rolling365)
        {
            var period = CountingWindow.For(rule, DateOnly.FromDateTime(end));
            var periodDays = CountCountry(_residency.CalculateResidencyDays(presence, period.Start, period.End), request.ArrivalCountry);
            return new StayMeasure(periodDays, periodDays, period.Start, period.End);
        }

        var window = _trips.GetForecastWindow(hypothetical);
        var endDays = CountCountry(_residency.CalculateResidencyDays(presence, window.Start, window.End), request.ArrivalCountry);
        var periodStart = DateOnly.FromDateTime(request.TripStart);
        if (periodStart > window.End)
        {
            periodStart = window.End;
        }

        var peaks = _residency.PeakRollingCounts(presence, periodStart, window.End);
        var peakDays = endDays;
        if (TryGetPeak(peaks, request.ArrivalCountry, out var peak) && peak.Days > peakDays)
        {
            peakDays = peak.Days;
        }

        return new StayMeasure(peakDays, endDays, window.Start, window.End);
    }

    public async Task<LocationOnDate> GetLocationOnAsync(string userId, DateOnly day, Traveller? traveller = null)
    {
        var trips = await _trips.GetAllTripsAsync(userId, traveller);
        return LocationFrom(trips, day);
    }

    public async Task<CurrentLocation> GetCurrentLocationAsync(string userId, Traveller? traveller = null)
    {
        var trips = await _trips.GetAllTripsAsync(userId, traveller);
        var now = DateTime.UtcNow;
        Trip? lastArrival = null;
        var lastArrivalUtc = DateTime.MinValue;
        Trip? inTransit = null;
        foreach (var trip in trips)
        {
            var arrivalUtc = ToUtc(trip.ArrivalDateTime, trip.ArrivalTimezone);
            var departureUtc = ToUtc(trip.DepartureDateTime, trip.DepartureTimezone);
            if (arrivalUtc <= now && arrivalUtc >= lastArrivalUtc)
            {
                lastArrival = trip;
                lastArrivalUtc = arrivalUtc;
            }

            if (departureUtc <= now && arrivalUtc > now)
            {
                inTransit = trip;
            }
        }

        if (lastArrival is null)
        {
            return new CurrentLocation(string.Empty, "noData", null, null, null, null, null, null, null);
        }

        var today = LocationFrom(trips, DateOnly.FromDateTime(DateTime.Today));
        var status = inTransit is not null ? "inTransit" : today.Status;
        var country = inTransit is not null
            ? "IN_TRANSIT"
            : string.IsNullOrEmpty(today.LocationAtMidnight) ? lastArrival.ArrivalCountry : today.LocationAtMidnight;
        return new CurrentLocation(
            country,
            status,
            lastArrival.ArrivalIataCode,
            lastArrival.ArrivalCity,
            lastArrival.ArrivalCountry,
            DateOnly.FromDateTime(lastArrival.ArrivalDateTime),
            inTransit?.ArrivalIataCode,
            inTransit?.ArrivalCity,
            inTransit?.ArrivalCountry);
    }

    public async Task<List<StandardDurationForecastItemDto>> GetStandardDurationForecastsAsync(
        string userId,
        StandardDurationForecastRequestDto request,
        IReadOnlyList<Trip>? additionalTrips = null,
        Traveller? traveller = null)
    {
        var limit = request.DayLimit <= 0 ? 183 : request.DayLimit;
        var durations = request.Durations is { Length: > 0 } ? request.Durations : DefaultDurations;
        var existing = new List<Trip>(await _trips.GetAllTripsAsync(userId, traveller));
        if (additionalTrips is { Count: > 0 })
        {
            existing.AddRange(additionalTrips);
        }

        var stay = new MaxTripEndDateRequestDto
        {
            DepartureCountry = request.DepartureCountry,
            DepartureCity = request.DepartureCity,
            DepartureTimezone = request.DepartureTimezone,
            DepartureIataCode = request.DepartureIataCode,
            ArrivalCountry = request.ArrivalCountry,
            ArrivalCity = request.ArrivalCity,
            ArrivalTimezone = request.ArrivalTimezone,
            ArrivalIataCode = request.ArrivalIataCode,
            TripStart = request.TripStart,
            DayLimit = limit
        };

        var results = new List<StandardDurationForecastItemDto>();
        foreach (var duration in durations)
        {
            var end = request.TripStart.AddDays(duration);
            var days = DaysInCountry(existing, stay, end);
            results.Add(new StandardDurationForecastItemDto
            {
                DurationDays = duration,
                EndDate = end,
                TotalDaysInCountry = days,
                ExceedsLimit = days > limit
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<RollingDay>> GetRollingCountAsync(
        string userId,
        string country,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Trip>? additionalTrips = null,
        Traveller? traveller = null)
    {
        var trips = new List<Trip>(await _trips.GetAllTripsAsync(userId, traveller));
        if (additionalTrips is { Count: > 0 })
        {
            trips.AddRange(additionalTrips);
        }

        var presence = _residency.GenerateDailyPresenceLog(trips);
        var counted = new HashSet<DateOnly>();
        foreach (var day in presence)
        {
            if (CountCountry(_residency.CalculateResidencyDays(presence, day.Date, day.Date), country) > 0)
            {
                counted.Add(day.Date);
            }
        }

        var rows = new List<RollingDay>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var start = RollingWindow.StartOn(date);
            var total = 0;
            for (var day = start; day <= date; day = day.AddDays(1))
            {
                if (counted.Contains(day))
                {
                    total++;
                }
            }

            var anniversary = date.AddDays(-RollingWindow.LengthDays);
            rows.Add(new RollingDay(date, total, anniversary, counted.Contains(anniversary)));
        }

        return rows;
    }

    public async Task<PresencePlan> PlanMaxPresenceAsync(
        string userId,
        string country,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<(DateOnly From, DateOnly To)> blackouts,
        int dayLimit,
        Traveller? traveller = null)
    {
        var limit = dayLimit <= 0 ? 183 : dayLimit;
        var rule = _residency.GetCountryRule(country);
        var presence = _residency.GenerateDailyPresenceLog(await _trips.GetAllTripsAsync(userId, traveller));
        var byDate = presence.ToDictionary(day => day.Date);
        var counted = new SortedSet<DateOnly>();
        foreach (var day in presence)
        {
            if (CountCountry(_residency.CalculateResidencyDays(presence, day.Date, day.Date), country) > 0)
            {
                counted.Add(day.Date);
            }
        }

        var added = new List<DateOnly>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (InRange(date, blackouts) || counted.Contains(date))
            {
                continue;
            }

            if (byDate.TryGetValue(date, out var existing)
                && !string.IsNullOrWhiteSpace(existing.LocationAtMidnight)
                && existing.LocationAtMidnight != "IN_TRANSIT"
                && !existing.LocationAtMidnight.Equals(country, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            counted.Add(date);
            if (ExceedsLimit(counted, rule, date, limit))
            {
                counted.Remove(date);
                continue;
            }

            added.Add(date);
        }

        var stays = new List<PlannedStay>();
        DateOnly? blockStart = null;
        DateOnly previous = default;
        foreach (var date in added)
        {
            if (blockStart is null)
            {
                blockStart = date;
            }
            else if (date != previous.AddDays(1))
            {
                stays.Add(new PlannedStay(blockStart.Value, previous, previous.DayNumber - blockStart.Value.DayNumber + 1));
                blockStart = date;
            }

            previous = date;
        }

        if (blockStart is not null)
        {
            stays.Add(new PlannedStay(blockStart.Value, previous, previous.DayNumber - blockStart.Value.DayNumber + 1));
        }

        var peak = 0;
        if (counted.Count > 0)
        {
            var first = counted.Min;
            var last = counted.Max.AddDays(364);
            for (var end = first; end <= last; end = end.AddDays(1))
            {
                peak = Math.Max(peak, CountInWindow(counted, rule, end));
            }
        }

        return new PresencePlan(country, limit, added.Count, peak, stays);
    }

    private static bool ExceedsLimit(SortedSet<DateOnly> counted, CountryResidencyRule rule, DateOnly added, int limit)
    {
        if (rule.WindowType == CountingWindowType.Rolling365)
        {
            for (var end = added; end <= added.AddDays(364); end = end.AddDays(1))
            {
                if (CountInWindow(counted, rule, end) > limit)
                {
                    return true;
                }
            }

            return false;
        }

        return CountInWindow(counted, rule, added) > limit;
    }

    private static int CountInWindow(SortedSet<DateOnly> counted, CountryResidencyRule rule, DateOnly referenceEnd)
    {
        var period = CountingWindow.For(rule, referenceEnd);
        return counted.GetViewBetween(period.Start, period.End).Count;
    }

    private static bool InRange(DateOnly date, IReadOnlyList<(DateOnly From, DateOnly To)> ranges)
    {
        foreach (var range in ranges)
        {
            var start = range.From <= range.To ? range.From : range.To;
            var end = range.From <= range.To ? range.To : range.From;
            if (date >= start && date <= end)
            {
                return true;
            }
        }

        return false;
    }

    private static MaxTripEndDateRequestDto WithStart(MaxTripEndDateRequestDto request, DateTime arrival) => new()
    {
        DepartureCountry = request.DepartureCountry,
        DepartureCity = request.DepartureCity,
        DepartureTimezone = request.DepartureTimezone,
        DepartureIataCode = request.DepartureIataCode,
        ArrivalCountry = request.ArrivalCountry,
        ArrivalCity = request.ArrivalCity,
        ArrivalTimezone = request.ArrivalTimezone,
        ArrivalIataCode = request.ArrivalIataCode,
        TripStart = arrival,
        DayLimit = request.DayLimit
    };

    private static IEnumerable<string> CountryNames(IEnumerable<DailyPresence> presence)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var day in presence)
        {
            if (!string.IsNullOrWhiteSpace(day.LocationAtMidnight) && day.LocationAtMidnight != "IN_TRANSIT")
            {
                names.Add(day.LocationAtMidnight);
            }

            foreach (var country in day.LocationsDuringDay)
            {
                if (!string.IsNullOrWhiteSpace(country) && country != "IN_TRANSIT")
                {
                    names.Add(country);
                }
            }
        }

        return names;
    }

    private int DaysInCountry(IReadOnlyList<Trip> existing, MaxTripEndDateRequestDto request, DateTime end) =>
        MeasureStay(existing, request, end).EndOfWindowDays;

    private LocationOnDate LocationFrom(IReadOnlyList<Trip> trips, DateOnly day)
    {
        var presence = _residency.GenerateDailyPresenceLog(trips.ToList());
        var match = presence.FirstOrDefault(item => item.Date == day && !string.IsNullOrWhiteSpace(item.LocationAtMidnight));
        if (match is not null)
        {
            return new LocationOnDate(
                day,
                match.LocationAtMidnight,
                match.LocationsDuringDay.ToList(),
                match.IsInTransitAtMidnight,
                IsTravelDay(trips, day) ? "recorded" : "gapFilled");
        }

        Trip? lastArrival = null;
        var lastArrivalUtc = DateTime.MinValue;
        foreach (var trip in trips)
        {
            if (DateOnly.FromDateTime(trip.ArrivalDateTime) > day || string.IsNullOrWhiteSpace(trip.ArrivalCountry))
            {
                continue;
            }

            var arrivalUtc = ToUtc(trip.ArrivalDateTime, trip.ArrivalTimezone);
            if (lastArrival is null || arrivalUtc >= lastArrivalUtc)
            {
                lastArrival = trip;
                lastArrivalUtc = arrivalUtc;
            }
        }

        if (lastArrival is not null)
        {
            return new LocationOnDate(day, lastArrival.ArrivalCountry, [lastArrival.ArrivalCountry], false, "projected");
        }

        return new LocationOnDate(day, string.Empty, [], false, "noData");
    }

    private static bool IsTravelDay(IEnumerable<Trip> trips, DateOnly day)
    {
        foreach (var trip in trips)
        {
            var departure = DateOnly.FromDateTime(trip.DepartureDateTime);
            var arrival = DateOnly.FromDateTime(trip.ArrivalDateTime);
            var start = departure < arrival ? departure : arrival;
            var end = departure < arrival ? arrival : departure;
            if (day >= start && day <= end)
            {
                return true;
            }
        }

        return false;
    }

    private static DateOnly EarliestLocalDate(Trip trip)
    {
        var departure = DateOnly.FromDateTime(trip.DepartureDateTime);
        var arrival = DateOnly.FromDateTime(trip.ArrivalDateTime);
        return departure < arrival ? departure : arrival;
    }

    private static int CountCountry(Dictionary<string, int> days, string country) =>
        days.Where(pair => pair.Key.Equals(country, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Value).FirstOrDefault();

    private static bool TryGetPeak(Dictionary<string, PeakCount> peaks, string country, out PeakCount peak)
    {
        if (peaks.TryGetValue(country, out peak))
        {
            return true;
        }

        foreach (var pair in peaks)
        {
            if (pair.Key.Equals(country, StringComparison.OrdinalIgnoreCase))
            {
                peak = pair.Value;
                return true;
            }
        }

        peak = default;
        return false;
    }

    private static DateTime ToUtc(DateTime local, string? timezoneId)
    {
        if (local.Kind == DateTimeKind.Utc)
        {
            return local;
        }

        var unspecified = local.Kind == DateTimeKind.Unspecified ? local : DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var zoneId = string.IsNullOrWhiteSpace(timezoneId) ? "UTC" : timezoneId;
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, zone);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTime.SpecifyKind(unspecified, DateTimeKind.Utc);
        }
        catch (InvalidTimeZoneException)
        {
            return DateTime.SpecifyKind(unspecified, DateTimeKind.Utc);
        }
    }

    private static List<Trip> Stay(MaxTripEndDateRequestDto request, DateTime end)
    {
        var start = DateTime.SpecifyKind(request.TripStart, DateTimeKind.Unspecified);
        var departureAt = DateTime.SpecifyKind(end, DateTimeKind.Unspecified);
        var outboundDeparture = start.AddHours(-6);
        var returnArrival = departureAt.AddHours(6);
        var departureZone = string.IsNullOrWhiteSpace(request.DepartureTimezone) ? "UTC" : request.DepartureTimezone;
        var arrivalZone = string.IsNullOrWhiteSpace(request.ArrivalTimezone) ? "UTC" : request.ArrivalTimezone;

        return
        [
            new Trip
            {
                DepartureCountry = request.DepartureCountry,
                DepartureCity = request.DepartureCity,
                DepartureDateTime = outboundDeparture,
                DepartureTimezone = departureZone,
                DepartureIataCode = request.DepartureIataCode,
                ArrivalCountry = request.ArrivalCountry,
                ArrivalCity = request.ArrivalCity,
                ArrivalDateTime = start,
                ArrivalTimezone = arrivalZone,
                ArrivalIataCode = request.ArrivalIataCode
            },
            new Trip
            {
                DepartureCountry = request.ArrivalCountry,
                DepartureCity = request.ArrivalCity,
                DepartureDateTime = departureAt,
                DepartureTimezone = arrivalZone,
                DepartureIataCode = request.ArrivalIataCode,
                ArrivalCountry = request.DepartureCountry,
                ArrivalCity = request.DepartureCity,
                ArrivalDateTime = returnArrival,
                ArrivalTimezone = departureZone,
                ArrivalIataCode = request.DepartureIataCode
            }
        ];
    }
}

public sealed record RecordCoverage(DateOnly? FirstTripDate, DateOnly? LastTripDate, int LegCount);

public sealed record CalendarYearPresence(
    string Country,
    int Year,
    int Days,
    DateOnly WindowStart,
    DateOnly WindowEnd,
    int? MinimumDays,
    int? DaysShort,
    string? Status,
    DateOnly? ProjectedFrom,
    string? ProjectedCountry);

public sealed record MinimumPresence(
    int MinimumDays,
    string WindowType,
    DateOnly WindowStart,
    DateOnly WindowEnd,
    int Days,
    int DaysShort,
    string Status);

public sealed record ForecastBreakdown(
    DateOnly WindowStart,
    DateOnly WindowEnd,
    Dictionary<string, int> Current,
    Dictionary<string, int> Forecast,
    Dictionary<string, PeakCount> Peaks);

public readonly record struct StayMeasure(int PeakDays, int EndOfWindowDays, DateOnly WindowStart, DateOnly WindowEnd);

public sealed record EarliestArrival(
    DateTime Arrival,
    int DaysAtLimit,
    DateOnly WindowStart,
    DateOnly WindowEnd,
    string RuleType,
    string WindowType);

public readonly record struct RollingDay(DateOnly Date, int RollingDays, DateOnly AnniversaryDate, bool AnniversaryCounted);

public sealed record PlannedStay(DateOnly Arrival, DateOnly Departure, int Days);

public sealed record PresencePlan(
    string Country,
    int DayLimit,
    int PlannedDays,
    int PeakDays,
    IReadOnlyList<PlannedStay> Stays);

public sealed record LocationOnDate(
    DateOnly Date,
    string LocationAtMidnight,
    IReadOnlyList<string> LocationsDuringDay,
    bool IsInTransitAtMidnight,
    string Status);

public sealed record CurrentLocation(
    string Country,
    string Status,
    string? LastArrivalIataCode,
    string? LastArrivalCity,
    string? LastArrivalCountry,
    DateOnly? LastArrivalDate,
    string? InTransitArrivalIataCode,
    string? InTransitArrivalCity,
    string? InTransitArrivalCountry);
