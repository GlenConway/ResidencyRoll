using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using Radzen;
using Radzen.Blazor;
using ResidencyRoll.Shared.Trips;
using ResidencyRoll.Web.Components.Dialogs;
using ResidencyRoll.Web.Data;
using ResidencyRoll.Web.Helpers;
using ResidencyRoll.Web.Services;
using System.Globalization;

namespace ResidencyRoll.Web.Components.Pages;

public partial class ManageTrips
{
    private List<TripDto> trips = new();
    private RadzenDataGrid<TripDto>? tripsGrid;
    private IBrowserFile? selectedFile;
    private bool importing;
    private string importMessage = string.Empty;
    private bool importError;
    private bool editingTrip = false;
    private TripDto? currentEditTrip;
    private DateTime? departureDate;
    private DateTime? departureTime;
    private DateTime? arrivalDate;
    private DateTime? arrivalTime;
    private int selectedTabIndex = 0; // 0 = Trip List, 1 = Trip Editor
    private string validationMessage = string.Empty;
    private string itineraryParsingError = string.Empty;
    private bool itineraryParsingSuccess;
    private int parsedTripsCount;
    private bool isItineraryParserAvailable;
    private bool isItineraryParserAvailabilityChecked;
    private bool showImportExport;
    private bool isAddingNewTrip;
    private List<TripDto> parsedDraftTrips = new();
    private int currentParsedDraftIndex = -1;

    [Inject] private TripsApiClient ApiClient { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private Microsoft.JSInterop.IJSRuntime JS { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private ILogger<ManageTrips> Logger { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
        await CheckItineraryParserAvailability();
    }

    private async Task CheckItineraryParserAvailability()
    {
        try
        {
            isItineraryParserAvailable = await ApiClient.IsItineraryParsingAvailableAsync();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to check itinerary parsing availability");
            isItineraryParserAvailable = false;
        }
        finally
        {
            isItineraryParserAvailabilityChecked = true;
        }
    }

    private async Task OpenItineraryParserDialog()
    {
        itineraryParsingSuccess = false;
        itineraryParsingError = string.Empty;

        var result = await DialogService.OpenAsync<ItineraryParserDialog>(
            "Quick Itinerary Parser",
            options: new DialogOptions
            {
                Width = "680px",
                Resizable = false,
                Draggable = false,
                CloseDialogOnOverlayClick = true
            });

        if (result is ItineraryParsingResponseDto response)
        {
            await ApplyParsedTripsAsync(response);
        }
    }

    private async Task ApplyParsedTripsAsync(ItineraryParsingResponseDto response)
    {
        try
        {
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

            var draftTrips = new List<TripDto>();

            foreach (var parsedLeg in response.Legs)
            {
                if (!DateTime.TryParseExact(parsedLeg.DepartureDatetimeLocal, "yyyy-MM-ddTHH:mm",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var departureDateTime))
                {
                    itineraryParsingError = $"Could not parse departure datetime: {parsedLeg.DepartureDatetimeLocal}";
                    Logger.LogWarning("Could not parse departure datetime: {DateTime}", parsedLeg.DepartureDatetimeLocal);
                    return;
                }

                var arrivalDateTime = departureDateTime;
                if (!string.IsNullOrWhiteSpace(parsedLeg.ArrivalDatetimeLocal) &&
                    DateTime.TryParseExact(parsedLeg.ArrivalDatetimeLocal, "yyyy-MM-ddTHH:mm",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedArrivalDateTime))
                {
                    arrivalDateTime = parsedArrivalDateTime;
                }

                var departureAirport = AirportDatabase.FindByIataCode(parsedLeg.DepartureAirport);
                var arrivalAirport = AirportDatabase.FindByIataCode(parsedLeg.ArrivalAirport);

                var trip = new TripDto
                {
                    DepartureCountry = departureAirport?.Country ?? parsedLeg.DepartureAirport,
                    DepartureCity = departureAirport?.City ?? parsedLeg.DepartureAirport,
                    DepartureTimezone = departureAirport?.IanaTimezone ?? "UTC",
                    DepartureIataCode = parsedLeg.DepartureAirport,
                    DepartureDateTime = departureDateTime,
                    ArrivalCountry = arrivalAirport?.Country ?? parsedLeg.ArrivalAirport,
                    ArrivalCity = arrivalAirport?.City ?? parsedLeg.ArrivalAirport,
                    ArrivalTimezone = arrivalAirport?.IanaTimezone ?? "UTC",
                    ArrivalIataCode = parsedLeg.ArrivalAirport,
                    ArrivalDateTime = arrivalDateTime
                };

                draftTrips.Add(trip);
            }

            parsedDraftTrips = draftTrips;
            currentParsedDraftIndex = 0;
            parsedTripsCount = parsedDraftTrips.Count;
            itineraryParsingSuccess = true;
            ApplyTripToEditor(parsedDraftTrips[currentParsedDraftIndex]);
        }
        catch (Exception ex)
        {
            itineraryParsingError = $"Error applying parsed itinerary: {ex.Message}";
            Logger.LogError(ex, "Exception while applying parsed itinerary to trips");
        }
    }

    private void SaveCurrentEditorToParsedDraft()
    {
        if (currentEditTrip == null || currentParsedDraftIndex < 0 || currentParsedDraftIndex >= parsedDraftTrips.Count)
            return;

        if (departureDate == null || departureTime == null || arrivalDate == null || arrivalTime == null)
            return;

        parsedDraftTrips[currentParsedDraftIndex] = new TripDto
        {
            DepartureCountry = currentEditTrip.DepartureCountry,
            DepartureCity = currentEditTrip.DepartureCity,
            DepartureTimezone = currentEditTrip.DepartureTimezone,
            DepartureIataCode = currentEditTrip.DepartureIataCode,
            DepartureDateTime = departureDate.Value.Date.Add(departureTime.Value.TimeOfDay),
            ArrivalCountry = currentEditTrip.ArrivalCountry,
            ArrivalCity = currentEditTrip.ArrivalCity,
            ArrivalTimezone = currentEditTrip.ArrivalTimezone,
            ArrivalIataCode = currentEditTrip.ArrivalIataCode,
            ArrivalDateTime = arrivalDate.Value.Date.Add(arrivalTime.Value.TimeOfDay)
        };
    }

    private void ApplyTripToEditor(TripDto trip)
    {
        currentEditTrip = new TripDto
        {
            DepartureCountry = trip.DepartureCountry,
            DepartureCity = trip.DepartureCity,
            DepartureTimezone = trip.DepartureTimezone,
            DepartureIataCode = trip.DepartureIataCode,
            DepartureDateTime = trip.DepartureDateTime,
            ArrivalCountry = trip.ArrivalCountry,
            ArrivalCity = trip.ArrivalCity,
            ArrivalTimezone = trip.ArrivalTimezone,
            ArrivalIataCode = trip.ArrivalIataCode,
            ArrivalDateTime = trip.ArrivalDateTime
        };

        departureDate = currentEditTrip.DepartureDateTime.Date;
        departureTime = currentEditTrip.DepartureDateTime;
        arrivalDate = currentEditTrip.ArrivalDateTime.Date;
        arrivalTime = currentEditTrip.ArrivalDateTime;
    }

    private void ShowPreviousParsedDraft()
    {
        if (currentParsedDraftIndex <= 0)
            return;

        SaveCurrentEditorToParsedDraft();
        currentParsedDraftIndex--;
        ApplyTripToEditor(parsedDraftTrips[currentParsedDraftIndex]);
    }

    private void ShowNextParsedDraft()
    {
        if (currentParsedDraftIndex >= parsedDraftTrips.Count - 1)
            return;

        SaveCurrentEditorToParsedDraft();
        currentParsedDraftIndex++;
        ApplyTripToEditor(parsedDraftTrips[currentParsedDraftIndex]);
    }

    private async Task SaveParsedDraftTrips()
    {
        validationMessage = string.Empty;

        if (parsedDraftTrips.Count == 0)
            return;

        SaveCurrentEditorToParsedDraft();

        foreach (var trip in parsedDraftTrips)
        {
            var missingFields = new List<string>();
            if (string.IsNullOrWhiteSpace(trip.DepartureCity)) missingFields.Add("Departure City");
            if (string.IsNullOrWhiteSpace(trip.DepartureCountry)) missingFields.Add("Departure Country");
            if (string.IsNullOrWhiteSpace(trip.DepartureTimezone)) missingFields.Add("Departure Timezone");
            if (string.IsNullOrWhiteSpace(trip.ArrivalCity)) missingFields.Add("Arrival City");
            if (string.IsNullOrWhiteSpace(trip.ArrivalCountry)) missingFields.Add("Arrival Country");
            if (string.IsNullOrWhiteSpace(trip.ArrivalTimezone)) missingFields.Add("Arrival Timezone");

            if (missingFields.Any())
            {
                validationMessage = $"Please complete all required fields before saving parsed legs. Missing: {string.Join(", ", missingFields)}";
                return;
            }
        }

        foreach (var trip in parsedDraftTrips)
        {
            await ApiClient.CreateTripAsync(trip);
        }

        await LoadData();
        ResetParsedDraftState();
        editingTrip = false;
        currentEditTrip = null;
        isAddingNewTrip = false;
        selectedTabIndex = 0;
    }

    private void ResetParsedDraftState()
    {
        parsedDraftTrips.Clear();
        currentParsedDraftIndex = -1;
        itineraryParsingSuccess = false;
        itineraryParsingError = string.Empty;
        parsedTripsCount = 0;
    }

    private async Task LoadData()
    {
        trips = (await ApiClient.GetAllTripsAsync()).OrderByDescending(t => t.DepartureDateTime).ToList();
    }

    private async Task OnFileSelected(InputFileChangeEventArgs args)
    {
        selectedFile = args.File;
        importMessage = string.Empty;
        importError = false;
        await Task.CompletedTask;
    }

    private void ToggleImportExport()
    {
        showImportExport = !showImportExport;
    }

    private async Task OnImport()
    {
        if (selectedFile == null)
        {
            return;
        }

        importing = true;
        importMessage = string.Empty;
        importError = false;

        try
        {
            using var stream = selectedFile.OpenReadStream(long.MaxValue);
            var (imported, message, errors) = await ApiClient.ImportTripsAsync(stream, selectedFile.Name);
            importMessage = message;
            importError = errors > 0 && imported == 0;
            await LoadData();
        }
        catch (Exception ex)
        {
            importMessage = $"Error: {ex.Message}";
            importError = true;
        }
        finally
        {
            importing = false;
        }
    }
    
    private async Task OnUpdateRow(TripDto trip)
    {
        await ApiClient.UpdateTripAsync(trip.Id, trip);
        await LoadData();
    }

    private async Task OnCreateRow(TripDto trip)
    {
        await ApiClient.CreateTripAsync(trip);
        await LoadData();
    }

    private async Task ExportTrips()
    {
        try
        {
            var (csvBytes, filename) = await ApiClient.ExportTripsAsync();
            var base64 = Convert.ToBase64String(csvBytes);
            await JS.InvokeVoidAsync("downloadFile", filename, "text/csv", base64);
        }
        catch (Exception ex)
        {
            // Could show error message to user
            Console.WriteLine($"Export failed: {ex.Message}");
        }
    }

    private void EditRow(TripDto trip)
    {
        ResetParsedDraftState();
        currentEditTrip = trip;
        isAddingNewTrip = false;
        editingTrip = true;
        selectedTabIndex = 1;
        
        // Initialize date/time fields
        departureDate = trip.DepartureDateTime.Date;
        departureTime = trip.DepartureDateTime;
        arrivalDate = trip.ArrivalDateTime.Date;
        arrivalTime = trip.ArrivalDateTime;
    }

    private async Task SaveRow(TripDto trip)
    {
        await tripsGrid!.UpdateRow(trip);
    }

    private void CancelEdit(TripDto trip)
    {
        tripsGrid!.CancelEditRow(trip);
        editingTrip = false;
    }

    private async Task DeleteRow(TripDto trip)
    {
        await ApiClient.DeleteTripAsync(trip.Id);
        await LoadData();
    }

    private void InsertRow()
    {
        ResetParsedDraftState();
        currentEditTrip = new TripDto 
        { 
            DepartureTimezone = "UTC",
            ArrivalTimezone = "UTC"
        };
        departureDate = DateTime.Today;
        departureTime = DateTime.Today.AddHours(12);
        arrivalDate = DateTime.Today;
        arrivalTime = DateTime.Today.AddHours(12);
        isAddingNewTrip = true;
        editingTrip = true;
        selectedTabIndex = 1;
    }

    private void OnDepartureDateChanged(DateTime? newDate)
    {
        departureDate = newDate;

        var isExistingTrip = currentEditTrip?.Id > 0;
        arrivalDate = TripDateHelper.GetSyncedArrivalDate(newDate, arrivalDate, isExistingTrip);
    }

    private void AddLeg(TripDto baseTrip)
    {
        ResetParsedDraftState();
        var nextLeg = TripLegFactory.CreateNextLeg(baseTrip);

        currentEditTrip = nextLeg;
        departureDate = nextLeg.DepartureDateTime.Date;
        departureTime = nextLeg.DepartureDateTime;
        arrivalDate = nextLeg.ArrivalDateTime.Date;
        arrivalTime = nextLeg.ArrivalDateTime;

        isAddingNewTrip = false;
        editingTrip = true;
        selectedTabIndex = 1;
    }

    private async Task SaveDetailedTrip(bool addLegAfterSave = false)
    {
        validationMessage = string.Empty;
        
        if (currentEditTrip == null || departureDate == null || departureTime == null || 
            arrivalDate == null || arrivalTime == null)
        {
            validationMessage = "Please fill in all date and time fields.";
            return;
        }

        // Validate required fields
        var missingFields = new List<string>();
        
        if (string.IsNullOrWhiteSpace(currentEditTrip.DepartureCity))
            missingFields.Add("Departure City");
        if (string.IsNullOrWhiteSpace(currentEditTrip.DepartureCountry))
            missingFields.Add("Departure Country");
        if (string.IsNullOrWhiteSpace(currentEditTrip.DepartureTimezone))
            missingFields.Add("Departure Timezone");
        if (string.IsNullOrWhiteSpace(currentEditTrip.ArrivalCity))
            missingFields.Add("Arrival City");
        if (string.IsNullOrWhiteSpace(currentEditTrip.ArrivalCountry))
            missingFields.Add("Arrival Country");
        if (string.IsNullOrWhiteSpace(currentEditTrip.ArrivalTimezone))
            missingFields.Add("Arrival Timezone");
        
        if (missingFields.Any())
        {
            validationMessage = $"Please complete the following required fields: {string.Join(", ", missingFields)}";
            return;
        }

        // Combine date and time
        currentEditTrip.DepartureDateTime = departureDate.Value.Date.Add(departureTime.Value.TimeOfDay);
        currentEditTrip.ArrivalDateTime = arrivalDate.Value.Date.Add(arrivalTime.Value.TimeOfDay);

        TripDto savedTrip;
        if (currentEditTrip.Id > 0)
        {
            await ApiClient.UpdateTripAsync(currentEditTrip.Id, currentEditTrip);
            // Fetch back from API to ensure we have the exact saved values
            savedTrip = await ApiClient.GetTripByIdAsync(currentEditTrip.Id) ?? currentEditTrip;
        }
        else
        {
            savedTrip = await ApiClient.CreateTripAsync(currentEditTrip);
            currentEditTrip.Id = savedTrip.Id;
        }

        if (addLegAfterSave)
        {
            AddLeg(savedTrip);
            await LoadData();
            return;
        }

        await LoadData();

        editingTrip = false;
        currentEditTrip = null;
        isAddingNewTrip = false;
        selectedTabIndex = 0; // Switch back to Trip List tab
        StateHasChanged();
    }

    private void CancelDetailedEdit()
    {
        ResetParsedDraftState();
        editingTrip = false;
        currentEditTrip = null;
        isAddingNewTrip = false;
        validationMessage = string.Empty;
        selectedTabIndex = 0;
    }

    private string GetTimezoneOffset(string timezoneId)
    {
        if (string.IsNullOrEmpty(timezoneId))
            return string.Empty;

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            var offset = tz.GetUtcOffset(DateTime.UtcNow);
            var sign = offset < TimeSpan.Zero ? "-" : "+";
            return $"UTC{sign}{Math.Abs(offset.Hours):D2}:{Math.Abs(offset.Minutes):D2}";
        }
        catch
        {
            return timezoneId;
        }
    }
}
