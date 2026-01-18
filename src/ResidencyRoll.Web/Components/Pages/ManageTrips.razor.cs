using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using ResidencyRoll.Shared.Trips;
using ResidencyRoll.Web.Helpers;
using ResidencyRoll.Web.Services;

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

    [Inject] private TripsApiClient ApiClient { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private Microsoft.JSInterop.IJSRuntime JS { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
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
        currentEditTrip = trip;
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
        currentEditTrip = new TripDto 
        { 
            DepartureTimezone = "UTC",
            ArrivalTimezone = "UTC"
        };
        departureDate = DateTime.Today;
        departureTime = DateTime.Today.AddHours(12);
        arrivalDate = DateTime.Today;
        arrivalTime = DateTime.Today.AddHours(12);
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
        var nextLeg = TripLegFactory.CreateNextLeg(baseTrip);

        currentEditTrip = nextLeg;
        departureDate = nextLeg.DepartureDateTime.Date;
        departureTime = nextLeg.DepartureDateTime;
        arrivalDate = nextLeg.ArrivalDateTime.Date;
        arrivalTime = nextLeg.ArrivalDateTime;

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
        selectedTabIndex = 0; // Switch back to Trip List tab
        StateHasChanged();
    }

    private void CancelDetailedEdit()
    {
        editingTrip = false;
        currentEditTrip = null;
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
