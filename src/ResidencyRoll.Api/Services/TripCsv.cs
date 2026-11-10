using System.Globalization;
using System.Text;
using ResidencyRoll.Api.Models;

namespace ResidencyRoll.Api.Services;

/// <summary>
/// The trip CSV used by the web import and by MCP. A header row is mapped by name.
/// Files without a header keep the older column order, and Source, BookingRef, and Notes
/// are read only when the header names them or when all fourteen columns are present.
/// </summary>
public static class TripCsv
{
    public const string Header =
        "DepartureCountry,DepartureCity,DepartureDateTime,DepartureTimezone,DepartureIataCode,ArrivalCountry,ArrivalCity,ArrivalDateTime,ArrivalTimezone,ArrivalIataCode,Traveller,Source,BookingRef,Notes";

    public sealed class Row
    {
        public Trip Trip { get; init; } = new();
        public string? TravellerName { get; init; }
    }

    public sealed class ParseResult
    {
        public List<Row> Rows { get; init; } = [];
        public List<string> Errors { get; init; } = [];
    }

    public static string Format(IEnumerable<Trip> trips, string travellerName)
    {
        var lines = new List<string> { Header };
        foreach (var trip in trips.OrderBy(trip => trip.DepartureDateTime))
        {
            lines.Add(string.Join(',',
                Escape(trip.DepartureCountry),
                Escape(trip.DepartureCity),
                trip.DepartureDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Escape(trip.DepartureTimezone),
                Escape(trip.DepartureIataCode),
                Escape(trip.ArrivalCountry),
                Escape(trip.ArrivalCity),
                trip.ArrivalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Escape(trip.ArrivalTimezone),
                Escape(trip.ArrivalIataCode),
                Escape(travellerName),
                Escape(trip.Source),
                Escape(trip.BookingRef),
                Escape(trip.Notes)));
        }

        return string.Join('\n', lines);
    }

    public static ParseResult Parse(string? content)
    {
        var rows = new List<Row>();
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add("The CSV is empty.");
            return new ParseResult { Rows = rows, Errors = errors };
        }

        var lines = content.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        Dictionary<string, int>? columns = null;
        var lineNumber = 0;
        foreach (var raw in lines)
        {
            lineNumber++;
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (columns is null && lineNumber == 1 && IsHeader(line))
            {
                columns = Index(ParseLine(line));
                continue;
            }

            var parts = ParseLine(line);
            if (!TryRow(parts, columns, lineNumber, out var row, out var error))
            {
                errors.Add(error!);
                continue;
            }

            rows.Add(row!);
        }

        if (rows.Count == 0 && errors.Count == 0)
        {
            errors.Add("The CSV has no trip rows.");
        }

        return new ParseResult { Rows = rows, Errors = errors };
    }

    public static string Escape(string? value)
    {
        var text = value ?? "";
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n'))
        {
            return '"' + text.Replace("\"", "\"\"") + '"';
        }

        return text;
    }

    private static bool IsHeader(string line) =>
        line.StartsWith("DepartureCountry", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("CountryName", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, int> Index(string[] headers)
    {
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Length; index++)
        {
            var name = headers[index].Trim();
            if (name.Length > 0 && !columns.ContainsKey(name))
            {
                columns[name] = index;
            }
        }

        return columns;
    }

    private static bool TryRow(string[] parts, Dictionary<string, int>? columns, int lineNumber, out Row? row, out string? error)
    {
        row = null;
        error = null;
        string? Field(string name)
        {
            if (columns is null || !columns.TryGetValue(name, out var index) || index >= parts.Length)
            {
                return null;
            }

            return parts[index];
        }

        if (columns is not null)
        {
            if (columns.ContainsKey("DepartureCountry"))
            {
                return TryFlight(
                    Field("DepartureCountry"),
                    Field("DepartureCity"),
                    Field("DepartureDateTime"),
                    Field("DepartureTimezone"),
                    Field("DepartureIataCode"),
                    Field("ArrivalCountry"),
                    Field("ArrivalCity"),
                    Field("ArrivalDateTime"),
                    Field("ArrivalTimezone"),
                    Field("ArrivalIataCode"),
                    Field("Traveller"),
                    Field("Source"),
                    Field("BookingRef"),
                    Field("Notes"),
                    lineNumber,
                    out row,
                    out error);
            }

            return TryLegacy(Field("CountryName") ?? parts.ElementAtOrDefault(0), Field("StartDate") ?? parts.ElementAtOrDefault(1), Field("EndDate") ?? parts.ElementAtOrDefault(2), lineNumber, out row, out error);
        }

        if (parts.Length >= 10)
        {
            var traveller = parts.Length >= 11 ? parts[10] : null;
            var source = parts.Length >= 14 ? parts[11] : null;
            var booking = parts.Length >= 14 ? parts[12] : null;
            var notes = parts.Length >= 14 ? parts[13] : null;
            return TryFlight(parts[0], parts[1], parts[2], parts[3], parts[4], parts[5], parts[6], parts[7], parts[8], parts[9], traveller, source, booking, notes, lineNumber, out row, out error);
        }

        if (parts.Length >= 8)
        {
            return TryFlight(parts[0], parts[1], parts[2], parts[3], null, parts[4], parts[5], parts[6], parts[7], null, null, null, null, null, lineNumber, out row, out error);
        }

        if (parts.Length >= 3)
        {
            return TryLegacy(parts[0], parts[1], parts[2], lineNumber, out row, out error);
        }

        error = $"Line {lineNumber} has {parts.Length} columns. Expected 8, 10, or a header row.";
        return false;
    }

    private static bool TryFlight(
        string? departureCountry,
        string? departureCity,
        string? departureTime,
        string? departureZone,
        string? departureIata,
        string? arrivalCountry,
        string? arrivalCity,
        string? arrivalTime,
        string? arrivalZone,
        string? arrivalIata,
        string? traveller,
        string? source,
        string? bookingRef,
        string? notes,
        int lineNumber,
        out Row? row,
        out string? error)
    {
        row = null;
        error = null;
        if (!DateTime.TryParse(departureTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var departure)
            || !DateTime.TryParse(arrivalTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var arrival))
        {
            error = $"Line {lineNumber} has a date that is not yyyy-MM-dd HH:mm:ss.";
            return false;
        }

        if (!IsTimezone(departureZone))
        {
            error = $"Line {lineNumber} has departure timezone '{departureZone}'. Use an IANA name such as America/Halifax.";
            return false;
        }

        if (!IsTimezone(arrivalZone))
        {
            error = $"Line {lineNumber} has arrival timezone '{arrivalZone}'. Use an IANA name such as America/Halifax.";
            return false;
        }

        string? sourceError = null;
        string? bookingError = null;
        string? notesError = null;
        if (!TripText.TryOptional(source, Trip.MaxSourceLength, "source", out var storedSource, out sourceError)
            || !TripText.TryOptional(bookingRef, Trip.MaxBookingRefLength, "bookingRef", out var storedBooking, out bookingError)
            || !TripText.TryOptional(notes, Trip.MaxNotesLength, "notes", out var storedNotes, out notesError))
        {
            error = $"Line {lineNumber}: {sourceError ?? bookingError ?? notesError}";
            return false;
        }

        row = new Row
        {
            TravellerName = string.IsNullOrWhiteSpace(traveller) ? null : traveller.Trim(),
            Trip = new Trip
            {
                DepartureCountry = departureCountry?.Trim() ?? "",
                DepartureCity = departureCity?.Trim() ?? "",
                DepartureDateTime = DateTime.SpecifyKind(departure, DateTimeKind.Unspecified),
                DepartureTimezone = departureZone!.Trim(),
                DepartureIataCode = Blank(departureIata),
                ArrivalCountry = arrivalCountry?.Trim() ?? "",
                ArrivalCity = arrivalCity?.Trim() ?? "",
                ArrivalDateTime = DateTime.SpecifyKind(arrival, DateTimeKind.Unspecified),
                ArrivalTimezone = arrivalZone!.Trim(),
                ArrivalIataCode = Blank(arrivalIata),
                Source = storedSource,
                BookingRef = storedBooking,
                Notes = storedNotes
            }
        };
        return true;
    }

    private static bool TryLegacy(string? country, string? start, string? end, int lineNumber, out Row? row, out string? error)
    {
        row = null;
        if (!DateTime.TryParse(start, CultureInfo.InvariantCulture, DateTimeStyles.None, out var arrival)
            || !DateTime.TryParse(end, CultureInfo.InvariantCulture, DateTimeStyles.None, out var departure))
        {
            error = $"Line {lineNumber} has a legacy date that is not yyyy-MM-dd HH:mm:ss.";
            return false;
        }

        row = new Row
        {
            Trip = new Trip
            {
                DepartureCountry = country?.Trim() ?? "",
                DepartureCity = "",
                DepartureDateTime = DateTime.SpecifyKind(departure, DateTimeKind.Unspecified),
                DepartureTimezone = "UTC",
                ArrivalCountry = country?.Trim() ?? "",
                ArrivalCity = "",
                ArrivalDateTime = DateTime.SpecifyKind(arrival, DateTimeKind.Unspecified),
                ArrivalTimezone = "UTC"
            }
        };
        error = null;
        return true;
    }

    private static bool IsTimezone(string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
        {
            return false;
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezone.Trim());
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string[] ParseLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        values.Add(current.ToString());
        return values.ToArray();
    }
}
