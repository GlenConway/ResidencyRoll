using System.ComponentModel;
using System.Globalization;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;

namespace ResidencyRoll.Api.Mcp;

public sealed class McpTripLeg
{
    [Description("Departure airport IATA code, such as YHZ. Optional when city, country, and timezone are set.")]
    public string? DepartureIataCode { get; set; }

    [Description("Departure city. Filled from the airport table when an IATA code is known. Required for airports that are not in the table.")]
    public string? DepartureCity { get; set; }

    [Description("Departure country. Filled from the airport table when an IATA code is known.")]
    public string? DepartureCountry { get; set; }

    [Description("Departure IANA timezone, such as America/Halifax. Filled from the airport table when an IATA code is known.")]
    public string? DepartureTimezone { get; set; }

    [Description("Local departure date and time, yyyy-MM-ddTHH:mm. Required when creating a leg. On update_trip, omit it to leave the saved departure time.")]
    public string? DepartureDateTime { get; set; }

    [Description("Arrival airport IATA code, such as LHR. Optional when city, country, and timezone are set.")]
    public string? ArrivalIataCode { get; set; }

    [Description("Arrival city. Filled from the airport table when an IATA code is known.")]
    public string? ArrivalCity { get; set; }

    [Description("Arrival country. Filled from the airport table when an IATA code is known.")]
    public string? ArrivalCountry { get; set; }

    [Description("Arrival IANA timezone, such as Europe/London. Filled from the airport table when an IATA code is known.")]
    public string? ArrivalTimezone { get; set; }

    [Description("Local arrival date and time, yyyy-MM-ddTHH:mm. Required when creating a leg. On update_trip, omit it to leave the saved arrival time.")]
    public string? ArrivalDateTime { get; set; }

    [Description("Where this leg came from, such as an email or a calendar. Optional. On update_trip, omit it to leave the saved value and pass an empty string to clear it.")]
    public string? Source { get; set; }

    [Description("Booking reference used to recognise the same leg on a later import. Optional. On update_trip, omit it to leave the saved value.")]
    public string? BookingRef { get; set; }

    [Description("Free-text note. Optional. On update_trip, omit it to leave the saved value.")]
    public string? Notes { get; set; }
}

public static class TripLegResolver
{
    public static List<Trip> ResolveAll(IReadOnlyList<McpTripLeg> legs, string userId, AirportCatalog airports)
    {
        if (legs.Count == 0)
        {
            throw new ModelContextProtocol.McpException("legs must contain at least one flight leg.");
        }

        var errors = new List<string>();
        var trips = new List<Trip>();
        for (var index = 0; index < legs.Count; index++)
        {
            try
            {
                trips.Add(Resolve(legs[index], userId, airports));
            }
            catch (ModelContextProtocol.McpException ex)
            {
                errors.Add($"Leg {index + 1}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            throw new ModelContextProtocol.McpException(string.Join("\n", errors));
        }

        return trips;
    }

    public static Trip Resolve(McpTripLeg leg, string userId, AirportCatalog airports)
    {
        var departure = ResolvePlace(leg.DepartureIataCode, leg.DepartureCity, leg.DepartureCountry, leg.DepartureTimezone, "Departure", airports);
        var arrival = ResolvePlace(leg.ArrivalIataCode, leg.ArrivalCity, leg.ArrivalCountry, leg.ArrivalTimezone, "Arrival", airports);
        return new Trip
        {
            UserId = userId,
            DepartureIataCode = departure.IataCode,
            DepartureCity = departure.City,
            DepartureCountry = departure.Country,
            DepartureTimezone = departure.Timezone,
            DepartureDateTime = ParseLocal(leg.DepartureDateTime, "departureDateTime"),
            ArrivalIataCode = arrival.IataCode,
            ArrivalCity = arrival.City,
            ArrivalCountry = arrival.Country,
            ArrivalTimezone = arrival.Timezone,
            ArrivalDateTime = ParseLocal(leg.ArrivalDateTime, "arrivalDateTime"),
            Source = Optional(leg.Source, Trip.MaxSourceLength, "source"),
            BookingRef = Optional(leg.BookingRef, Trip.MaxBookingRefLength, "bookingRef"),
            Notes = Optional(leg.Notes, Trip.MaxNotesLength, "notes")
        };
    }

    public static void ApplyPatch(Trip existing, McpTripLeg changes, AirportCatalog airports)
    {
        if (HasPlace(changes.DepartureIataCode, changes.DepartureCity, changes.DepartureCountry, changes.DepartureTimezone))
        {
            var replacingAirport = !string.IsNullOrWhiteSpace(changes.DepartureIataCode);
            var departure = ResolvePlace(
                replacingAirport ? changes.DepartureIataCode : existing.DepartureIataCode,
                changes.DepartureCity,
                changes.DepartureCountry ?? (replacingAirport ? null : existing.DepartureCountry),
                changes.DepartureTimezone ?? (replacingAirport ? null : existing.DepartureTimezone),
                "Departure",
                airports);
            existing.DepartureIataCode = departure.IataCode ?? existing.DepartureIataCode;
            existing.DepartureCity = departure.City;
            existing.DepartureCountry = departure.Country;
            existing.DepartureTimezone = departure.Timezone;
        }

        if (HasPlace(changes.ArrivalIataCode, changes.ArrivalCity, changes.ArrivalCountry, changes.ArrivalTimezone))
        {
            var replacingAirport = !string.IsNullOrWhiteSpace(changes.ArrivalIataCode);
            var arrival = ResolvePlace(
                replacingAirport ? changes.ArrivalIataCode : existing.ArrivalIataCode,
                changes.ArrivalCity,
                changes.ArrivalCountry ?? (replacingAirport ? null : existing.ArrivalCountry),
                changes.ArrivalTimezone ?? (replacingAirport ? null : existing.ArrivalTimezone),
                "Arrival",
                airports);
            existing.ArrivalIataCode = arrival.IataCode ?? existing.ArrivalIataCode;
            existing.ArrivalCity = arrival.City;
            existing.ArrivalCountry = arrival.Country;
            existing.ArrivalTimezone = arrival.Timezone;
        }

        if (!string.IsNullOrWhiteSpace(changes.DepartureDateTime))
        {
            existing.DepartureDateTime = ParseLocal(changes.DepartureDateTime, "departureDateTime");
        }

        if (!string.IsNullOrWhiteSpace(changes.ArrivalDateTime))
        {
            existing.ArrivalDateTime = ParseLocal(changes.ArrivalDateTime, "arrivalDateTime");
        }

        if (changes.Source is not null)
        {
            existing.Source = Optional(changes.Source, Trip.MaxSourceLength, "source");
        }

        if (changes.BookingRef is not null)
        {
            existing.BookingRef = Optional(changes.BookingRef, Trip.MaxBookingRefLength, "bookingRef");
        }

        if (changes.Notes is not null)
        {
            existing.Notes = Optional(changes.Notes, Trip.MaxNotesLength, "notes");
        }
    }

    private static bool HasPlace(string? iata, string? city, string? country, string? timezone) =>
        !string.IsNullOrWhiteSpace(iata)
        || !string.IsNullOrWhiteSpace(city)
        || !string.IsNullOrWhiteSpace(country)
        || !string.IsNullOrWhiteSpace(timezone);

    public static (string? IataCode, string City, string Country, string Timezone) ResolvePlace(
        string? iataCode,
        string? city,
        string? country,
        string? timezone,
        string label,
        AirportCatalog airports)
    {
        var code = string.IsNullOrWhiteSpace(iataCode) ? null : iataCode.Trim().ToUpperInvariant();
        var known = airports.FindByIataCode(code);
        if (code is not null && known is null && (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(country) || string.IsNullOrWhiteSpace(timezone)))
        {
            throw new ModelContextProtocol.McpException(
                $"Airport '{code}' is not in the airport table. Provide {label.ToLowerInvariant()} city, country, and timezone.");
        }

        var resolvedCity = First(city, known?.City);
        var resolvedCountry = First(country, known?.Country);
        var resolvedZone = First(timezone, known?.IanaTimezone);
        if (string.IsNullOrWhiteSpace(resolvedCity) || string.IsNullOrWhiteSpace(resolvedCountry) || string.IsNullOrWhiteSpace(resolvedZone))
        {
            throw new ModelContextProtocol.McpException(
                $"{label} needs an IATA code from the airport table, or an explicit city, country, and IANA timezone.");
        }

        return (code, resolvedCity, resolvedCountry, RequireTimezone(resolvedZone, label));
    }

    public static DateTime ParseLocal(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ModelContextProtocol.McpException($"{name} is required. Use yyyy-MM-ddTHH:mm.");
        }

        var formats = new[] { "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm" };
        if (!DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            throw new ModelContextProtocol.McpException($"{name} '{value}' must be a local date-time in yyyy-MM-ddTHH:mm.");
        }

        return DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
    }

    public static DateOnly ParseDate(string value, string name)
    {
        if (!DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new ModelContextProtocol.McpException($"{name} '{value}' must be yyyy-MM-dd.");
        }

        return date;
    }

    private static string RequireTimezone(string timezone, string label)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return timezone;
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ModelContextProtocol.McpException($"Invalid {label.ToLowerInvariant()} timezone '{timezone}'. Use an IANA name such as America/Halifax.");
        }
        catch (InvalidTimeZoneException)
        {
            throw new ModelContextProtocol.McpException($"Invalid {label.ToLowerInvariant()} timezone '{timezone}'. Use an IANA name such as America/Halifax.");
        }
    }

    private static string? Optional(string? value, int maxLength, string name)
    {
        if (!TripText.TryOptional(value, maxLength, name, out var stored, out var error))
        {
            throw new ModelContextProtocol.McpException(error!);
        }

        return stored;
    }

    private static string? First(string? explicitValue, string? fallback) =>
        string.IsNullOrWhiteSpace(explicitValue) ? fallback : explicitValue.Trim();
}
