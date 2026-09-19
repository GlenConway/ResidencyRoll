# Daily Presence Gap-Filling Rules

## Overview
When a user has trips in the system, there may be gaps in the daily presence log for dates between trips. These gaps must be intelligently filled based on where the person was physically present.

## Core Principle
**A person remains in their arrival location until they depart for another trip.**

For example:
- Arrive Auckland Jan 6 → Person is in Auckland Jan 6, 7, 8, 9 (until departure)
- Depart Auckland Jan 9, Arrive Sydney Jan 9 → Person is in Sydney from Jan 9 onwards

## Gap-Filling Scenarios

### Scenario 1: Trip with Intermediate Days
**User's Timeline:**
- Jan 6: Fly Sydney → Auckland (arrive 4:50pm)
- Jan 7-8: Stay in Auckland (no trips)
- Jan 9: Fly Auckland → Sydney (depart 2:10pm, arrive 3:55pm)

**Expected Daily Presence:**
- Jan 6: **New Zealand** (arrived in Auckland)
- Jan 7: **New Zealand** (full day, already in Auckland)
- Jan 8: **New Zealand** (full day, still in Auckland)
- Jan 9: **Australia** (departed Auckland, arrived Sydney)
- Jan 10+: **Australia** (stayed in Sydney)

### Scenario 2: Gap Between Trips
**User's Timeline:**
- Jan 6: Arrive in Country A
- Jan 7-14: No trips (gap in data)
- Jan 15: Depart Country A

**Expected Daily Presence:**
- Jan 6: **Country A** (arrival date)
- Jan 7-14: **Country A** (filled based on Jan 6 arrival)
- Jan 15: **Country A** (departure date, still in Country A)

## Implementation Rules

### Rule 1: Preserve Existing Presence Records
Do NOT overwrite presence records that already exist in the daily presence log. The service's `GenerateDailyPresenceLog()` and `FillGapsBetweenTrips()` methods already handle interior gaps.

### Rule 2: Fill Gaps Between Consecutive Records ONLY
Only fill gaps that exist between two consecutive presence records.

**Valid Gap:** Jan 6 has data, Jan 9 has data, but Jan 7-8 are missing
- **Action:** Fill Jan 7-8 with Jan 6's location

**Invalid Gap (Already Handled):** Jan 6, 7, 8, 9 all have data
- **Action:** Do NOT fill anything

### Rule 3: Use LocationAtMidnight, Not LocationsDuringDay
When filling gaps, use `LocationAtMidnight` from the preceding presence record, NOT `LocationsDuringDay`.

**Why:** LocationAtMidnight represents the primary location for that date and is consistent with how the Midnight Rule works.

### Rule 4: Never Create Data Before First Record or After Last Record
Only fill gaps BETWEEN consecutive records. Do not extend the presence log before the first trip or after the last trip.

**Examples:**
- If presence starts Jan 6, do NOT create records for Jan 1-5
- If presence ends Jan 9, do NOT create records for Jan 10-15 (unless a later trip defines them)

## Sydney-Auckland Test Case

### Input Trips
```
Trip 1:
  Departure: Jan 6, 11:45 AM (Sydney, Australia/Sydney timezone)
  Arrival:   Jan 6, 4:50 PM (Auckland, Pacific/Auckland timezone)

Trip 2:
  Departure: Jan 9, 2:10 PM (Auckland, Pacific/Auckland timezone)
  Arrival:   Jan 9, 3:55 PM (Sydney, Australia/Sydney timezone)
```

### Expected GenerateDailyPresenceLog Output
(After ProcessTripForDailyPresence + FillGapsBetweenTrips)
```
Jan 6: LocationAtMidnight=New Zealand, LocationsDuringDay=[Australia, New Zealand]
Jan 7: LocationAtMidnight=New Zealand, LocationsDuringDay=[New Zealand]
Jan 8: LocationAtMidnight=New Zealand, LocationsDuringDay=[New Zealand]
Jan 9: LocationAtMidnight=Australia, LocationsDuringDay=[New Zealand, Australia]
```

### Expected GetDailyPresence API Response
(Same as GenerateDailyPresenceLog, no gaps to fill)
```
Jan 6: LocationAtMidnight=New Zealand, LocationsDuringDay=[Australia, New Zealand]
Jan 7: LocationAtMidnight=New Zealand, LocationsDuringDay=[New Zealand]
Jan 8: LocationAtMidnight=New Zealand, LocationsDuringDay=[New Zealand]
Jan 9: LocationAtMidnight=Australia, LocationsDuringDay=[New Zealand, Australia]
```

### If User Requests Date Range Dec 25 - Jan 15
The presence log would have:
```
Dec 25-Jan 5: [No trip data - don't create]
Jan 6-9: [Trip data as above]
Jan 10-15: [No trip data - don't create]
```

**Result:** Only Jan 6-9 are returned. Days before/after trips are NOT fabricated.

## Debugging Checklist

When gap-filling produces incorrect results:

1. ✓ Verify `GenerateDailyPresenceLog()` returns correct data for trip dates
2. ✓ Verify `FillGapsBetweenTrips()` correctly fills intermediate days
3. ✓ Verify API controller gap-filling logic only fills gaps BETWEEN consecutive records
4. ✓ Verify LocationAtMidnight is used (not LocationsDuringDay) for gap-filled records
5. ✓ Verify no records are created before the first trip or after the last trip
6. ✓ Verify the UI correctly renders LocationsDuringDay when available (for multi-country days)

## Related Code

- **Service:** `ResidencyCalculationService.GenerateDailyPresenceLog()`
- **Service Helper:** `ResidencyCalculationService.FillGapsBetweenTrips()`
- **API Endpoint:** `TripsController.GetDailyPresence()`
- **UI Component:** `ResidencyTimeline.razor`
- **UI Logic:** `ResidencyTimeline.razor.cs` - `LoadData()` and day rendering logic
