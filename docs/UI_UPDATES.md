# UI Updates for Midnight-Rule Residency Tracking

## Overview
This document describes the UI updates made to support timezone-aware residency tracking and the "Daily Presence Log" backend logic implemented in the ResidencyCalculationService.

## 1. New Input Fields - Timezone-Aware Trip Entry

### TimezoneSelector Component
**Location:** `src/ResidencyRoll.Web/Components/TimezoneSelector.razor`

**Features:**
- City/Timezone search component for both Departure and Arrival locations
- Pre-populated database of 100+ major cities with IANA timezone identifiers
- Real-time filtering by city or country name
- Displays timezone offset (e.g., "Vancouver, Canada (UTC-08:00)")
- Two-way binding for City, Country, and Timezone properties

**Supported Cities Include:**
- North America: Major US, Canadian, and Mexican cities
- Europe: All major European capitals and cities
- Asia: Tokyo, Seoul, Beijing, Shanghai, Hong Kong, Singapore, Bangkok, Dubai, etc.
- Oceania: Sydney, Melbourne, Brisbane, Perth, Auckland, Wellington
- South America: São Paulo, Rio de Janeiro, Buenos Aires, Santiago
- Africa: Cairo, Johannesburg, Cape Town, Lagos, Nairobi

**Technical Implementation:**
- Uses Radzen Blazor components for UI consistency
- Encapsulates timezone logic in reusable component
- Validates IANA timezone strings through .NET TimeZoneInfo
- Provides EventCallback for parent component integration

### ManageTrips Page Updates
**Location:** `src/ResidencyRoll.Web/Components/Pages/ManageTrips.razor`

**New Features:**
1. **Dual Entry Modes:**
   - **Simple Mode:** Quick entry with legacy fields (Country, Start Date, End Date)
   - **Timezone Mode:** Full timezone-aware entry (Departure/Arrival City, DateTime, Timezone)

2. **Trip List Enhancements:**
   - Visual indicators ("Legacy" badge) for trips without timezone data
   - Three action buttons per trip:
     - Edit (pencil icon) - Simple edit mode
     - Schedule (clock icon) - Timezone edit mode
     - Delete (trash icon) - Remove trip

3. **Detailed Trip Editor Tab:**
   - Separate tab for timezone-aware trip entry
   - Split date/time pickers for departure and arrival
   - TimezoneSelector integration for both departure and arrival locations
   - Automatic legacy field population for backward compatibility

4. **Data Validation:**
   - Combines separate date and time inputs into single DateTime
   - Validates timezone selections
   - Ensures both departure and arrival information is complete

## 2. Residency Timeline Component

### ResidencyTimeline Component
**Location:** `src/ResidencyRoll.Web/Components/ResidencyTimeline.razor`

**Visual Features:**
- **Calendar Grid View:** Days organized by month with visual indicators
- **Color Coding:**
  - 🟢 Green: Day with country residency (location at midnight)
  - 🟠 Orange: Transit day (no country residency claimed)
  - ⬜ Gray/Dashed: Skipped day (IDL westbound crossing)

- **Interactive Elements:**
  - Click any day to see detailed information
  - Hover tooltips show date and location
  - Date range filters for custom analysis periods

- **IDL Visualization:**
  - Westbound crossings (e.g., Canada → Australia) show "skipped" calendar dates
  - Visual representation helps users understand date line effects
  - Tooltip explains: "Skipped day (IDL westbound crossing)"

**Technical Implementation:**
- Calls `/api/v1/trips/daily-presence` endpoint
- Automatically fills gaps in data to show IDL skips
- Groups days by month for organized display
- Responsive grid layout (auto-fill columns)
- CSS-based styling for performance

**User Experience:**
- Default: Shows last 365 days
- Filterable by start/end date
- Shows loading spinner during data fetch
- Selected day details panel shows:
  - Location at midnight
  - All countries visited during that day
  - Transit status

## 3. Summary Dashboard

### ResidencyStatusDashboard Component
**Location:** `src/ResidencyRoll.Web/Components/ResidencyStatusDashboard.razor`

**Features:**
1. **Country Cards:**
   - Large, prominent display of residency days
   - Shows country name and total days
   - Badge indicating rule type (Midnight vs. Partial Day)
   - Progress bar toward threshold (183 days)

2. **Visual Indicators:**
   - 🟢 Green border: Safe (> 30 days remaining)
   - 🟠 Orange border: Approaching threshold (≤ 30 days remaining)
   - 🔴 Red border: Over threshold

3. **Status Badges:**
   - ⚠️ Warning: "X days left" when approaching threshold
   - ⛔ Danger: "Over Threshold" when exceeded
   - ✓ Success: "X days remaining" when safe

4. **Rule Type Labels:**
   - **Midnight Rule:** "Counts days where present at midnight local time"
   - **Partial Day Rule:** "Counts any day with presence (partial day rule)"

5. **Legacy Data Warning:**
   - Alert banner if any trips lack timezone data
   - Suggests editing trips to add city/timezone information

**Technical Implementation:**
- Calls `/api/v1/trips/residency-summary` endpoint
- Calculates days for last 365 days by default
- Applies country-specific rules (Midnight vs. Partial Day)
- Responsive card grid (12 columns, 6 on medium, 4 on large screens)
- Hover animation (lift effect)

**Countries with Configured Rules:**
- **Midnight Rule (183 days):** Canada, UK, Australia, New Zealand
- **Partial Day Rule (183 days):** USA

## 4. Home Page Integration

### Updated Home Page
**Location:** `src/ResidencyRoll.Web/Components/Pages/Home.razor`

**New Layout:**
1. **ResidencyStatusDashboard** (Top) - Main feature
2. **ResidencyTimeline** (Middle) - Visual calendar
3. **Legacy Charts** (Bottom) - Existing distribution and per-country charts
4. **Trip Timeline** (Bottom) - Existing trip list

**Benefits:**
- New features prominently displayed
- Legacy functionality preserved
- Maintains backward compatibility
- Progressive enhancement approach

## 5. Technical Constraints & Implementation

### Browser-Based Calculations
The UI displays real-time calculations but relies on server-side logic for accuracy:

1. **Midnight Transitions:**
   - Server calculates midnight in both departure and arrival timezones
   - Handles International Date Line scenarios
   - UI displays results only (does not recalculate)

2. **Why Server-Side?**
   - Timezone database complexity (IANA data)
   - IDL crossing edge cases require sophisticated logic
   - Consistent calculations across all platforms
   - Avoids timezone data synchronization issues

3. **UI Responsibilities:**
   - Collect departure/arrival city, datetime, timezone
   - Validate input completeness
   - Display daily presence and residency summaries
   - Provide interactive filtering and details

### Backward Compatibility

**Legacy Data Handling:**
- Trips without timezone data still work
- `TripDto.HasTimezoneData` property indicates data quality
- Legacy fields (CountryName, StartDate, EndDate) preserved
- Mapping layer in `TripMapping.cs` handles conversion
- Visual indicators ("Legacy" badge) alert users

**Migration Path:**
- Users can edit existing trips to add timezone data
- No forced migration required
- Gradual enhancement as users update trips
- Dual entry modes support both workflows

## 6. API Endpoints

### New Endpoints Added

#### GET `/api/v1/trips/daily-presence`
**Query Parameters:**
- `startDate` (optional): Filter start date (yyyy-MM-dd)
- `endDate` (optional): Filter end date (yyyy-MM-dd)

**Returns:** `List<DailyPresenceDto>`
```json
[
  {
    "date": "2026-01-15",
    "locationAtMidnight": "Canada",
    "locationsDuringDay": ["Canada"],
    "isInTransitAtMidnight": false
  }
]
```

#### GET `/api/v1/trips/residency-summary`
**Query Parameters:**
- `startDate` (optional): Filter start date
- `endDate` (optional): Filter end date

**Returns:** `List<ResidencySummaryDto>`
```json
[
  {
    "countryName": "Canada",
    "totalDays": 150,
    "ruleType": "Midnight",
    "thresholdDays": 183,
    "isApproachingThreshold": false,
    "daysUntilThreshold": 33
  }
]
```

## 7. Files Modified/Created

### New Files
- `src/ResidencyRoll.Shared/Trips/DailyPresenceDto.cs`
- `src/ResidencyRoll.Shared/Trips/ResidencySummaryDto.cs`
- `src/ResidencyRoll.Shared/Trips/TimezoneLocation.cs`
- `src/ResidencyRoll.Web/Components/TimezoneSelector.razor`
- `src/ResidencyRoll.Web/Components/ResidencyTimeline.razor`
- `src/ResidencyRoll.Web/Components/ResidencyStatusDashboard.razor`
- `docs/UI_UPDATES.md` (this file)

### Modified Files
- `src/ResidencyRoll.Shared/Trips/TripDto.cs` - Added timezone fields
- `src/ResidencyRoll.Api/Controllers/TripsController.cs` - Added new endpoints
- `src/ResidencyRoll.Api/Mappings/TripMapping.cs` - Updated to handle timezone fields
- `src/ResidencyRoll.Api/Program.cs` - Registered ResidencyCalculationService
- `src/ResidencyRoll.Web/Services/TripsApiClient.cs` - Added new endpoint methods
- `src/ResidencyRoll.Web/Components/Pages/Home.razor` - Integrated new components
- `src/ResidencyRoll.Web/Components/Pages/ManageTrips.razor` - Major overhaul with dual modes

## 8. Testing the UI

### Manual Testing Steps

1. **Test Simple Trip Entry:**
   ```
   - Navigate to /manage-trips
   - Click "Add Simple Trip"
   - Select country, start date, end date
   - Verify trip appears in list with "Legacy" badge
   ```

2. **Test Timezone Trip Entry:**
   ```
   - Click "Add Trip with Timezone"
   - Search for departure city (e.g., "Vancouver")
   - Select timezone from dropdown
   - Set departure date/time
   - Repeat for arrival location
   - Verify trip saves with timezone data
   ```

3. **Test Residency Dashboard:**
   ```
   - Navigate to home page
   - Verify country cards display
   - Check progress bars reflect correct percentages
   - Verify color coding (green/orange/red)
   - Check rule type badges (Midnight vs Partial Day)
   ```

4. **Test Timeline:**
   ```
   - Scroll to ResidencyTimeline component
   - Verify days are color-coded correctly
   - Click a day to see details
   - Test date range filters
   - Verify IDL skip days show as dashed gray boxes
   ```

5. **Test IDL Scenarios:**
   ```
   - Create westbound trip (Vancouver → Sydney)
   - Verify timeline shows skipped calendar day
   - Create eastbound trip (Sydney → Vancouver)
   - Verify no days are skipped
   ```

## 9. Future Enhancements

### Potential Improvements
1. **Client-Side Preview:**
   - Show estimated midnight calculations before saving
   - Validate timezone selections
   - Preview daily presence for new trip

2. **Bulk Edit:**
   - Add timezone data to multiple legacy trips
   - Import timezone data from external sources

3. **Advanced Filtering:**
   - Filter by country on timeline
   - Show only transit days
   - Highlight specific date ranges

4. **Export Features:**
   - Export daily presence as CSV
   - Generate residency reports for tax purposes
   - Print-friendly timeline view

5. **Mobile Optimization:**
   - Touch-friendly date selection
   - Swipe navigation for timeline
   - Responsive card layouts

6. **Notifications:**
   - Alert when approaching threshold
   - Remind to update legacy trips
   - Suggest optimal travel dates

## Implementation Date
January 17, 2026

## Notes
- All UI components use Radzen Blazor for consistency
- Server-side rendering (InteractiveServer) required for real-time updates
- IANA timezone identifiers ensure cross-platform compatibility
- Backward compatibility maintained throughout
