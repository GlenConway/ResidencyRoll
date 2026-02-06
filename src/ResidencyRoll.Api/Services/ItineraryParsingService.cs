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

For each flight leg, return ONLY a valid JSON array with the following structure:
[
  {{
    ""departure_airport"": ""IATA CODE"",
    ""departure_datetime_local"": ""ISO 8601 format (e.g., 2026-01-23T16:40)"",
    ""arrival_airport"": ""IATA CODE""
  }}
]

Rules:
1. Extract ONLY the essential flight information (departure airport, departure time, arrival airport)
2. Ignore non-flight data such as seat numbers, passenger names, cabin class, and airline branding
3. Use IATA airport codes (e.g., YHZ, JFK, YUL, LHR)
4. Use ISO 8601 format for departure time in local timezone (e.g., 2026-01-23T16:40)
5. If required fields are missing for a leg, omit that leg entirely
6. Return ONLY the JSON array, with no explanatory text before or after
7. Legs should be in chronological order
8. If no valid flight legs can be extracted, return an empty array: []

Itinerary text:
{itineraryText}";
    }

    /// <summary>
    /// Parses the JSON response from the OpenAI model.
    /// </summary>
    private static ItineraryParsingResponseDto ParseJsonResponse(string responseText)
    {
        var result = new ItineraryParsingResponseDto();

        try
        {
            // Try to parse the response as a JSON array
            using (var jsonDoc = JsonDocument.Parse(responseText))
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

            return new ItineraryFlightLegDto
            {
                DepartureAirport = departureAirport,
                DepartureDatetimeLocal = departureDateTime,
                ArrivalAirport = arrivalAirport
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
