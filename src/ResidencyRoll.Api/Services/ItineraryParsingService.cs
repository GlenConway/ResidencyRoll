using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ResidencyRoll.Api.Configuration;
using ResidencyRoll.Shared.Trips;
using Serilog;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace ResidencyRoll.Api.Services;

/// <summary>
/// Service for parsing free-form flight itinerary text using OpenAI via Semantic Kernel.
/// Extracts structured flight leg information from unstructured text.
/// </summary>
public class ItineraryParsingService
{
    private readonly OpenAIOptions _options;
    private static readonly Serilog.ILogger Logger = Log.ForContext<ItineraryParsingService>();

    public ItineraryParsingService(IOptions<OpenAIOptions> options)
    {
        _options = options.Value;
    }

    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(_options.OpenAIModel) &&
               !string.IsNullOrWhiteSpace(_options.OpenAIEndpoint) &&
               !string.IsNullOrWhiteSpace(_options.OpenAIApiKey);
    }

    /// <summary>
    /// Parses a free-form flight itinerary text and extracts structured flight legs.
    /// Uses OpenAI chat completion to understand and extract the flight information.
    /// </summary>
    /// <param name="itineraryText">The raw itinerary text to parse</param>
    /// <returns>A DTO containing the extracted flight legs or an error message</returns>
    public async Task<ItineraryParsingResponseDto> ParseItineraryAsync(string itineraryText)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(itineraryText))
        {
            return new ItineraryParsingResponseDto
            {
                Error = "Itinerary text cannot be empty"
            };
        }

        try
        {
            Logger.Information("Starting itinerary parsing for text of length {Length}", itineraryText.Length);

            // Create the Semantic Kernel instance with OpenAI chat completion
#pragma warning disable SKEXP0010
            var kernel = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(
                    modelId: _options.OpenAIModel,
                    endpoint: new Uri(_options.OpenAIEndpoint),
                    apiKey: _options.OpenAIApiKey)
                .Build();
#pragma warning restore SKEXP0010

            // Get the chat completion service
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

            // Build the prompt with clear instructions
            var prompt = BuildItineraryParsingPrompt(itineraryText);

            // Create chat history and invoke the model
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(prompt);

            var response = await chatCompletionService.GetChatMessageContentAsync(
                chatHistory);

            var responseText = response.Content ?? string.Empty;
            Logger.Information("Received response from OpenAI: {Response}", responseText);

            // Parse the JSON response
            var result = ParseJsonResponse(responseText);

            Logger.Information("Successfully parsed {LegCount} flight legs from itinerary", result.Legs.Count);
            return result;
        }
        catch (HttpRequestException ex)
        {
            Logger.Error(ex, "HTTP error while calling OpenAI API");
            return new ItineraryParsingResponseDto
            {
                Error = $"Failed to call OpenAI API: {ex.Message}"
            };
        }
        catch (JsonException ex)
        {
            Logger.Error(ex, "Failed to parse OpenAI response as JSON");
            return new ItineraryParsingResponseDto
            {
                Error = $"Invalid JSON response from model: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unexpected error while parsing itinerary");
            return new ItineraryParsingResponseDto
            {
                Error = $"Unexpected error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Builds the prompt for the Semantic Kernel to parse flight itineraries.
    /// </summary>
    private static string BuildItineraryParsingPrompt(string itineraryText)
    {
        return $@"Extract flight leg information from the following itinerary text.

Return ONLY a valid JSON array with the structure:
[
  {{
    ""departure_airport"": ""IATA CODE"",
    ""departure_datetime_local"": ""ISO 8601 format (e.g., 2026-01-23T16:40)"",
    ""arrival_airport"": ""IATA CODE"",
    ""arrival_datetime_local"": ""ISO 8601 format (e.g., 2026-01-23T18:40)""
  }}
]

CRITICAL EXTRACTION RULES:
1. Each flight leg MUST include:
   - departure airport
   - departure date and time
   - arrival airport
   - arrival date and time
2. Arrival time MUST be explicitly extracted from the text.
   - DO NOT infer or estimate arrival time.
   - DO NOT guess based on flight duration.
3. If arrival date or arrival time cannot be explicitly found in the text, OMIT THE ENTIRE LEG.

PARSING RULES:
4. Ignore non-flight data such as seat numbers, passenger names, cabin class, airline branding, and column headers.
5. Use IATA airport codes extracted from parentheses (e.g., ""(YHZ)"", ""(LHR)"").
6. Dates may appear on their own line before or after the airport name
   (e.g., ""Friday, January 23"" or ""Saturday, January 24"").
7. Times may appear on a separate line and may include text like ""local time"".
   Extract the HH:MM portion only.
8. Combine date + time into ISO 8601 local datetime:
   - Example: ""Friday, January 23"" + ""16:40"" → ""2026-01-23T16:40"".
9. Arrival date and arrival time usually appear immediately after the arrival airport,
   possibly separated by blank lines — continue scanning until found.
10. Preserve the chronological order of legs.

OUTPUT RULES:
11. Return ONLY the JSON array — no markdown, no explanations.
12. If no valid flight legs are found, return: [].

Itinerary text:
{itineraryText}";
    }

    /// <summary>
    /// Cleans the JSON response by removing markdown code block formatting.
    /// Handles patterns like ```json ... ``` or ``` ... ```
    /// </summary>
    private static string CleanJsonResponse(string responseText)
    {
        // Remove markdown code block markers
        var cleaned = responseText.Trim();

        // Remove ```json or ``` at the start
        if (cleaned.StartsWith("```json"))
        {
            cleaned = cleaned[7..].Trim();
        }
        else if (cleaned.StartsWith("```"))
        {
            cleaned = cleaned[3..].Trim();
        }

        // Remove ``` at the end
        if (cleaned.EndsWith("```"))
        {
            cleaned = cleaned[..^3].Trim();
        }

        return cleaned;
    }

    /// <summary>
    /// Parses the JSON response from the OpenAI model.
    /// Handles responses wrapped in markdown code blocks.
    /// </summary>
    private static ItineraryParsingResponseDto ParseJsonResponse(string responseText)
    {
        var result = new ItineraryParsingResponseDto();

        try
        {
            // Clean the response text (remove markdown code blocks if present)
            var cleanedJson = CleanJsonResponse(responseText);

            // Try to parse the response as a JSON array
            using (var jsonDoc = JsonDocument.Parse(cleanedJson))
            {
                var root = jsonDoc.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in root.EnumerateArray())
                    {
                        if (element.ValueKind == JsonValueKind.Object)
                        {
                            var leg = ParseFlightLeg(element);
                            if (leg != null)
                            {
                                result.Legs.Add(leg);
                            }
                        }
                    }
                }
            }

            if (result.Legs.Count == 0)
            {
                Log.ForContext<ItineraryParsingService>().Warning("No valid flight legs parsed from response: {Response}", responseText);
            }

            return result;
        }
        catch (JsonException ex)
        {
            Log.ForContext<ItineraryParsingService>().Error(ex, "Failed to parse JSON response: {Response}", responseText);
            throw;
        }
    }

    /// <summary>
    /// Parses a single flight leg JSON element.
    /// </summary>
    private static ItineraryFlightLegDto? ParseFlightLeg(JsonElement element)
    {
        try
        {
            // Extract required fields
            var departureAirport = element.TryGetProperty("departure_airport", out var depAirport)
                ? depAirport.GetString()?.ToUpperInvariant()
                : null;

            var departureDateTime = element.TryGetProperty("departure_datetime_local", out var depTime)
                ? depTime.GetString()
                : null;

            var arrivalAirport = element.TryGetProperty("arrival_airport", out var arrAirport)
                ? arrAirport.GetString()?.ToUpperInvariant()
                : null;

            // Extract optional arrival datetime
            var arrivalDateTime = element.TryGetProperty("arrival_datetime_local", out var arrTime)
                ? arrTime.GetString()
                : null;

            // Validate required fields
            if (string.IsNullOrWhiteSpace(departureAirport) ||
                string.IsNullOrWhiteSpace(departureDateTime) ||
                string.IsNullOrWhiteSpace(arrivalAirport))
            {
                Log.ForContext<ItineraryParsingService>().Debug(
                    "Skipping incomplete flight leg: departure={Departure}, time={Time}, arrival={Arrival}",
                    departureAirport ?? "MISSING",
                    departureDateTime ?? "MISSING",
                    arrivalAirport ?? "MISSING");
                return null;
            }

            // Validate ISO 8601 format (basic validation)
            if (!IsValidISO8601DateTime(departureDateTime))
            {
                Log.ForContext<ItineraryParsingService>().Debug(
                    "Skipping flight leg with invalid datetime format: {DateTime}",
                    departureDateTime);
                return null;
            }

            // Validate arrival datetime if provided
            if (!string.IsNullOrWhiteSpace(arrivalDateTime) && !IsValidISO8601DateTime(arrivalDateTime))
            {
                Log.ForContext<ItineraryParsingService>().Debug(
                    "Invalid arrival datetime format (will use placeholder): {DateTime}",
                    arrivalDateTime);
                arrivalDateTime = null; // Will let UI provide a default if needed
            }

            return new ItineraryFlightLegDto
            {
                DepartureAirport = departureAirport,
                DepartureDatetimeLocal = departureDateTime,
                ArrivalAirport = arrivalAirport,
                ArrivalDatetimeLocal = arrivalDateTime
            };
        }
        catch (Exception ex)
        {
            Log.ForContext<ItineraryParsingService>().Debug(ex, "Failed to parse flight leg from element");
            return null;
        }
    }

    /// <summary>
    /// Validates if a string is in ISO 8601 format (basic validation).
    /// Accepts formats like: 2026-01-23T16:40, 2026-01-23T16:40:30, etc.
    /// </summary>
    private static bool IsValidISO8601DateTime(string dateTime)
    {
        if (string.IsNullOrWhiteSpace(dateTime))
            return false;

        // Basic check for ISO 8601 format: YYYY-MM-DDTHH:MM or similar
        return dateTime.Length >= 16 && // Minimum length for 2026-01-23T16:40
               dateTime[4] == '-' &&
               dateTime[7] == '-' &&
               dateTime[10] == 'T' &&
               dateTime[13] == ':';
    }
}
