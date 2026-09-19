# Flight Itinerary Parsing Feature

## Overview

This feature adds the ability to parse free-form flight itinerary text from users and extract structured flight leg information using OpenAI via Microsoft Semantic Kernel. Users can paste itinerary text (from emails, booking confirmations, etc.) and the system will automatically extract departure airports, departure times, and arrival airports.

## Architecture

### Components

- **ItineraryParsingService** (`src/ResidencyRoll.Api/Services/ItineraryParsingService.cs`)
  - Manages the Semantic Kernel integration with OpenAI
  - Builds and executes the parsing prompt
  - Parses JSON responses from the model
  - Handles errors gracefully

- **API Endpoint** (`src/ResidencyRoll.Api/Controllers/TripsController.cs`)
  - `POST /api/v1/trips/parse-itinerary`
  - Requires JWT authentication (via `[Authorize]`)
  - Accepts `ItineraryParsingRequestDto`
  - Returns `ItineraryParsingResponseDto`

- **DTOs** (`src/ResidencyRoll.Shared/Trips/`)
  - `ItineraryParsingRequestDto`: Request containing raw itinerary text
  - `ItineraryParsingResponseDto`: Response with extracted flight legs and optional error
  - `ItineraryFlightLegDto`: Individual flight leg with departure/arrival info

- **Configuration** (`src/ResidencyRoll.Api/Configuration/OpenAIOptions.cs`)
  - Configurable OpenAI model, endpoint, and API key
  - Loaded from appsettings via dependency injection

## Configuration

### Setup

Add the following to your `appsettings.json` or `appsettings.Development.json`:

```json
{
  "OpenAI": {
    "OpenAIModel": "gpt-4",
    "OpenAIEndpoint": "https://api.openai.com/v1",
    "OpenAIApiKey": "sk-your-api-key-here"
  }
}
```

**Environment Variables (for production):**
Set via environment variables instead:
- `OpenAI__OpenAIModel`
- `OpenAI__OpenAIEndpoint`
- `OpenAI__OpenAIApiKey`

### Supported Models

- `gpt-4` (recommended for accuracy)
- `gpt-4-turbo`
- `gpt-3.5-turbo` (faster, lower cost)

### Endpoint Configuration

**OpenAI Direct:**
```
https://api.openai.com/v1
```

**Azure OpenAI:**
```
https://{your-resource}.openai.azure.com/
```

## API Specification

### Request

**Endpoint:** `POST /api/v1/trips/parse-itinerary`

**Headers:**
```
Authorization: Bearer <jwt-token>
Content-Type: application/json
```

**Body:**
```json
{
  "itineraryText": "AC099 YHZ 4:40PM -> YUL 6:40PM..."
}
```

### Response (Success)

**Status:** `200 OK`

```json
{
  "legs": [
    {
      "departure_airport": "YHZ",
      "departure_datetime_local": "2026-01-23T16:40",
      "arrival_airport": "YUL",
      "arrival_datetime_local": "2026-01-23T18:40"
    },
    {
      "departure_airport": "YUL",
      "departure_datetime_local": "2026-01-24T10:30",
      "arrival_airport": "LHR",
      "arrival_datetime_local": "2026-01-24T21:15"
    }
  ],
  "error": null
}
```

### Response (Error)

**Status:** `200 OK` (with error field set)

```json
{
  "legs": [],
  "error": "Failed to call OpenAI API: Connection timeout"
}
```

## Examples

### Example 1: Simple Email Confirmation

**Request:**
```http
POST /api/v1/trips/parse-itinerary
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
Content-Type: application/json

{
  "itineraryText": "Flight Confirmation\n\nPassenger: John Doe\nConfirmation: ABC123XYZ\n\nOutbound\nAC099 from Halifax (YHZ) to Montreal (YUL)\nDeparture: January 23, 2026 - 4:40 PM\nArrival: January 23, 2026 - 6:40 PM  \nSeat: 12A\nCabin: Economy\n\nReturn\nAC100 from Montreal (YUL) to Halifax (YHZ)\nDeparture: January 30, 2026 - 2:15 PM\nArrival: January 30, 2026 - 4:15 PM\nSeat: 14B"
}
```

**Response:**
```json
{
  "legs": [
    {
      "departure_airport": "YHZ",
      "departure_datetime_local": "2026-01-23T16:40",
      "arrival_airport": "YUL",
      "arrival_datetime_local": "2026-01-23T18:40"
    },
    {
      "departure_airport": "YUL",
      "departure_datetime_local": "2026-01-30T14:15",
      "arrival_airport": "YHZ",
      "arrival_datetime_local": "2026-01-30T16:15"
    }
  ],
  "error": null
}
```

### Example 2: Multi-Leg International Trip

**Request:**
```http
POST /api/v1/trips/parse-itinerary
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
Content-Type: application/json

{
  "itineraryText": "ITINERARY\n\nLeg 1:\nYHZ -> YUL\nAir Canada AC099\n2026-01-23 16:40\n\nLeg 2:\nYUL -> LHR (London Heathrow)\nAir Canada AC865\nDeparture: 2026-01-24 10:30\n\nLeg 3:\nLHR -> CDG (Paris)\nBA308\nDate: Jan 25, 2026\nTime: 2:45pm local\n\nLeg 4:\nReturning home\nAF358\nCDG to YHZ\n2026-02-02 11:00"
}
```

**Response:**
```json
{
  "legs": [
    {
      "departure_airport": "YHZ",
      "departure_datetime_local": "2026-01-23T16:40",
      "arrival_airport": "YUL",
      "arrival_datetime_local": "2026-01-23T18:40"
    },
    {
      "departure_airport": "YUL",
      "departure_datetime_local": "2026-01-24T10:30",
      "arrival_airport": "LHR",
      "arrival_datetime_local": "2026-01-24T21:15"
    },
    {
      "departure_airport": "LHR",
      "departure_datetime_local": "2026-01-25T14:45",
      "arrival_airport": "CDG",
      "arrival_datetime_local": "2026-01-25T16:45"
    },
    {
      "departure_airport": "CDG",
      "departure_datetime_local": "2026-02-02T11:00",
      "arrival_airport": "YHZ",
      "arrival_datetime_local": "2026-02-02T13:00"
    }
  ],
  "error": null
}
```

### Example 3: Error Handling

**Request with empty text:**
```json
{
  "itineraryText": ""
}
```

**Response:**
```json
{
  "legs": [],
  "error": "Itinerary text cannot be empty"
}
```

## Implementation Details

### Prompt Strategy

The service uses a carefully crafted prompt that:

1. **Guides the model** to extract complete flight information
2. **Specifies the exact JSON format** for the response with all required fields
3. **Requests both departure and arrival times** in ISO 8601 format
4. **Instructs the model to infer arrival time** if not explicitly stated (typical flight duration, or next day if overnight)
5. **Ignores non-flight data** (seat numbers, passenger names, cabin class, etc.)
6. **Validates required fields** and omits incomplete legs
7. **Enforces JSON-only output** with no explanatory text

### Processing Flow

```
User Input → Validation → Semantic Kernel → OpenAI
              ↓
          Prompt Building
              ↓
         Chat Completion
              ↓
         JSON Parsing → Response Building → Return to User
```

### Error Handling

- **Empty input**: Returns error immediately
- **API errors**: Logs and returns error message
- **Invalid JSON**: Logs and attempts to extract valid legs
- **Incomplete legs**: Silently omits from results
- **Invalid timestamps**: Logs individually and continues processing

### Logging

All operations are logged at appropriate levels:
- **Information**: Start/completion of parsing, leg count
- **Error**: API failures, JSON parsing errors
- **Debug**: Individual leg validation, timestamp validation

Logs are written to both console and daily rotating files in `logs/api-*.log`

## Testing

### Manual Testing with cURL

```bash
curl -X POST https://localhost:7000/api/v1/trips/parse-itinerary \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "itineraryText": "Flight AC099 from YHZ to YUL on 2026-01-23 at 4:40 PM"
  }'
```

### Unit Testing

The service includes:
- Input validation
- JSON parsing validation
- Error handling for all exception types
- Logging verification (use test doubles)

## Dependencies

- **Microsoft.SemanticKernel** (v1.21.0 or higher)
  - Core framework for AI operations
  - Chat completion service
  - Kernel builder

- **Microsoft.Extensions.Options**
  - Configuration injection
  - Options pattern for OpenAI settings

- **Serilog**
  - Structured logging with ForContext

## Future Enhancements

Potential improvements:

1. **Timezone Support**
   - Extract timezone information from itinerary
   - Convert local times to ISO 8601 with timezone offset

2. **Caching**
   - Cache OpenAI responses for identical inputs
   - Reduce API costs

3. **Batch Processing**
   - Process multiple itineraries in a single API call
   - Better throughput for bulk operations

4. **Validation Enhancements**
   - Validate airport codes against a real airport database
   - Check that arrival times are after departure times
   - Detect impossible flight durations

5. **Additional Field Extraction**
   - Airline codes
   - Flight numbers
   - Aircraft types (if available)
   - Terminal information

6. **UI Integration**
   - Integrate with Blazor frontend Forecast page
   - Real-time parsing feedback
   - Suggested corrections for ambiguous data

## Troubleshooting

### Issue: 401 Unauthorized

**Cause:** Missing or invalid JWT token

**Solution:** 
- Ensure you're including the Authorization header
- Check that your token hasn't expired
- Verify JWT configuration in appsettings

### Issue: 429 Too Many Requests

**Cause:** OpenAI API rate limiting

**Solution:**
- Implement exponential backoff retry logic
- Cache responses to reduce API calls
- Contact OpenAI to increase rate limits

### Issue: Empty results from API

**Cause:** Model unable to extract structured data from itinerary format

**Solution:**
- Ensure itinerary text contains clear date/time information
- Use standard IATA airport codes (not city names)
- Check OpenAI model response in logs for details

### Issue: Incorrect time parsing

**Cause:** API key not configured or timestamp format unclear

**Solution:**
- Verify OpenAI configuration in appsettings
- Include full date and time in itinerary text
- Use 24-hour format or include AM/PM indicators

## References

- [Microsoft Semantic Kernel Documentation](https://learn.microsoft.com/en-us/semantic-kernel/)
- [OpenAI API Documentation](https://platform.openai.com/docs/)
- [IATA Airport Codes](https://www.iata.org/en/publications/directories/code-search/)
- [ISO 8601 Date and Time Format](https://en.wikipedia.org/wiki/ISO_8601)
