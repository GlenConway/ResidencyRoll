# Residency Midnight Logic - Implementation Summary

## Overview

This document outlines the refactored residency tracking system based on "location-at-midnight" logic, replacing the previous duration-based approach.

## Core Models

### 1. Daily Presence (`DailyPresence.cs`)
Represents physical presence on a specific calendar date:
- `Date`: The calendar date
- `LocationAtMidnight`: Country where physically present at 00:00 local time
- `LocationsDuringDay`: All countries visited during any part of the day (for Partial Day Rule)
- `IsInTransitAtMidnight`: Whether in transit (international waters/airspace) at midnight

### 2. Residency Rules (`ResidencyRuleType.cs`, `CountryResidencyRule.cs`)
- **Midnight Rule**: Day counts only if present at midnight (Canada, UK, Australia, etc.)
- **Partial Day Rule**: Day counts if present ANY part of the day (USA - Substantial Presence Test)

### 3. Trip Model Updates (`Trip.cs`)
Enhanced to include:
- Departure: Country, City, DateTime, Timezone (IANA format)
- Arrival: Country, City, DateTime, Timezone (IANA format)
- Legacy fields maintained for backward compatibility

## Calculation Logic

### Key Principle
We analyze EACH calendar date and determine:
1. Where was the person at midnight (00:00) in that timezone?
2. Which countries were visited during any part of that day?

### Process Flow
1. **Generate Daily Presence Log**: For each trip, determine the location at midnight for each relevant date
2. **Apply Country Rules**: Count days based on each country's specific residency rule
3. **Handle Overlaps**: Later trips take precedence for overlapping dates

### Timezone Handling
- All calculations use UTC as the absolute timeline
- Convert local times to UTC for comparison
- Check midnight in BOTH departure and arrival timezones (for IDL scenarios)

### International Date Line (IDL)
- **Westbound** (e.g., Canada → Australia): Calendar date may be "skipped" - marked as IN_TRANSIT
- **Eastbound** (e.g., Australia → Canada): No days skipped, but careful not to double-count midnights

## Test Scenarios

### 1. Australia Leap (IDL Westbound)
- Depart Canada Dec 23 → Arrive Australia Dec 25
- Expected: Canada +1 (Dec 23), Transit +1 (Dec 24), Australia +1 (Dec 25)

### 2. US Partial Day
- Arrive USA 11:50 PM Friday → Depart 2:00 AM Saturday
- Expected: 2 days (parts of both Friday and Saturday)

### 3. UK Midnight Rule
- Same timing as #2 but for UK
- Expected: 1 day (only the midnight between Friday/Saturday)

## Database Migration

Migration `AddTimezoneFieldsToTrips` adds:
- `DepartureCountry`, `DepartureCity`, `DepartureDateTime`, `DepartureTimezone`
- `ArrivalCountry`, `ArrivalCity`, `ArrivalDateTime`, `ArrivalTimezone`
- Migrates existing data from legacy fields (`CountryName`, `StartDate`, `EndDate`)

## API Usage

```csharp
var service = new ResidencyCalculationService();

// Generate daily presence log
var dailyLog = service.GenerateDailyPresenceLog(trips);

// Calculate residency days by country
var residencyDays = service.CalculateResidencyDays(dailyLog);

// Get days for specific country (with special rules)
var usaDays = service.CalculateResidencyDaysForCountry("USA", dailyLog, trips);

// Get location at specific timestamp
var location = service.GetLocationAtTimestamp(utcTimestamp, trips);
```

## Implementation Date
January 17, 2026

## Notes
- The legacy `DurationDays` property is marked as `[Obsolete]` but maintained for backward compatibility
- Transit days (IN_TRANSIT) do not count toward any country's residency
- USA transit exception (< 24 hours between foreign points) is recognized but simplified in initial implementation
