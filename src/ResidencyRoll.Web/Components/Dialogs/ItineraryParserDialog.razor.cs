using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;
using ResidencyRoll.Shared.Trips;
using ResidencyRoll.Web.Services;

namespace ResidencyRoll.Web.Components.Dialogs;

public partial class ItineraryParserDialog
{
    private string itineraryText = string.Empty;
    private string itineraryParsingError = string.Empty;
    private bool isParsing = false;

    [Inject] private TripsApiClient ApiClient { get; set; } = default!;
    [Inject] private ILogger<ItineraryParserDialog> Logger { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;

    private async Task ParseItinerary()
    {
        try
        {
            itineraryParsingError = string.Empty;
            isParsing = true;

            if (string.IsNullOrWhiteSpace(itineraryText))
            {
                itineraryParsingError = "Please paste some itinerary text first.";
                return;
            }

            Logger.LogInformation("Parsing itinerary text");

            var response = await ApiClient.ParseItineraryAsync(itineraryText);

            if (!string.IsNullOrEmpty(response.Error))
            {
                itineraryParsingError = response.Error;
                Logger.LogWarning("Itinerary parsing error: {Error}", response.Error);
                return;
            }

            if (response.Legs.Count == 0)
            {
                itineraryParsingError = "No flight legs could be extracted from the provided itinerary text. Please check the format and try again.";
                Logger.LogWarning("No legs parsed from itinerary");
                return;
            }

            DialogService.Close(response);
        }
        catch (Exception ex)
        {
            itineraryParsingError = $"Error parsing itinerary: {ex.Message}";
            Logger.LogError(ex, "Exception while parsing itinerary");
        }
        finally
        {
            isParsing = false;
        }
    }

    private void ClearText()
    {
        itineraryText = string.Empty;
        itineraryParsingError = string.Empty;
    }

    private void CloseDialog()
    {
        DialogService.Close(null);
    }
}
