using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Api.Data;
using ResidencyRoll.Api.Models;

namespace ResidencyRoll.Api.Services;

public readonly record struct CopyTripsResult(int Copied, int Skipped);

public class TravellerService
{
    private readonly ApplicationDbContext _context;
    private readonly TravellerAccess _access;

    public TravellerService(ApplicationDbContext context, TravellerAccess access)
    {
        _context = context;
        _access = access;
    }

    public async Task<List<Traveller>> ListAsync(string userId)
    {
        await _access.ResolveSelfAsync(userId);
        return await _access.ListAccessibleAsync(userId);
    }

    public Task<Traveller?> GetAsync(string userId, int id) => _access.FindOwnedAsync(userId, id);

    public async Task<Traveller> CreateAsync(string userId, string name, string? homeCountry)
    {
        var normalized = NormalizeName(name);
        var country = NormalizeCountry(homeCountry);
        await EnsureNameAvailable(userId, normalized, exceptId: null);
        var traveller = new Traveller
        {
            OwnerUserId = userId,
            Name = normalized,
            HomeCountry = country,
            CreatedUtc = DateTime.UtcNow
        };
        _context.Travellers.Add(traveller);
        await _context.SaveChangesAsync();
        return traveller;
    }

    public async Task<Traveller> UpdateAsync(string userId, int id, string name, string? homeCountry)
    {
        var traveller = await _access.FindOwnedAsync(userId, id)
            ?? throw new TravellerResolutionException("Traveller not found.");
        var normalized = NormalizeName(name);
        var country = NormalizeCountry(homeCountry);
        await EnsureNameAvailable(userId, normalized, traveller.Id);
        traveller.Name = normalized;
        traveller.HomeCountry = country;
        await _context.SaveChangesAsync();
        return traveller;
    }

    public async Task<bool> DeleteAsync(string userId, int id, bool deleteTrips)
    {
        var traveller = await _access.FindOwnedAsync(userId, id);
        if (traveller is null)
        {
            return false;
        }

        if (string.Equals(traveller.LinkedUserId, userId, StringComparison.Ordinal))
        {
            throw new TravellerConflictException("The signed-in traveller cannot be deleted.");
        }

        var trips = await _access.TripsFor(traveller).ToListAsync();
        if (trips.Count > 0 && !deleteTrips)
        {
            throw new TravellerConflictException("This traveller has trips. Pass deleteTrips=true to delete them too.");
        }

        if (trips.Count > 0)
        {
            _context.Trips.RemoveRange(trips);
        }

        _context.Travellers.Remove(traveller);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Traveller> ResolveAsync(string userId, string? traveller)
    {
        var accessible = await ListAsync(userId);
        if (string.IsNullOrWhiteSpace(traveller))
        {
            return accessible.First(item => string.Equals(item.LinkedUserId, userId, StringComparison.Ordinal));
        }

        var token = traveller.Trim();
        if (int.TryParse(token, out var id))
        {
            var byId = accessible.FirstOrDefault(item => item.Id == id);
            if (byId is not null)
            {
                return byId;
            }
        }

        var matches = accessible
            .Where(item => item.Name.Equals(token, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matches.Count == 1)
        {
            return matches[0];
        }

        var choices = accessible.Count == 0
            ? "No travellers."
            : "Travellers: " + string.Join(", ", accessible.Select(item => item.Name)) + ".";
        var lead = matches.Count == 0
            ? $"No traveller matches \"{token}\"."
            : $"More than one traveller matches \"{token}\".";
        throw new TravellerResolutionException($"{lead} {choices}");
    }

    public async Task<IReadOnlyList<Traveller>> ResolveManyAsync(string userId, IReadOnlyList<string>? travellers)
    {
        if (travellers is null || travellers.Count == 0)
        {
            return [await ResolveAsync(userId, null)];
        }

        var resolved = new List<Traveller>();
        foreach (var token in travellers)
        {
            var traveller = await ResolveAsync(userId, token);
            if (resolved.All(item => item.Id != traveller.Id))
            {
                resolved.Add(traveller);
            }
        }

        return resolved;
    }

    public async Task<CopyTripsResult> CopyTripsAsync(string userId, int sourceId, int targetId)
    {
        var source = await _access.FindOwnedAsync(userId, sourceId)
            ?? throw new TravellerResolutionException("Traveller not found.");
        var target = await _access.FindOwnedAsync(userId, targetId)
            ?? throw new TravellerResolutionException("Traveller not found.");
        var sourceTrips = await _access.TripsFor(source).ToListAsync();
        var targetTrips = await _access.TripsFor(target).ToListAsync();
        var copies = new List<Trip>();
        var skipped = 0;
        foreach (var trip in sourceTrips)
        {
            if (targetTrips.Any(existing => SameLeg(existing, trip)) || copies.Any(existing => SameLeg(existing, trip)))
            {
                skipped++;
                continue;
            }

            copies.Add(TripCopies.Onto(trip, target, userId));
        }

        if (copies.Count > 0)
        {
            _context.Trips.AddRange(copies);
            await _context.SaveChangesAsync();
        }

        return new CopyTripsResult(copies.Count, skipped);
    }

    public static bool SameLeg(Trip left, Trip right) =>
        left.DepartureDateTime == right.DepartureDateTime
        && left.ArrivalDateTime == right.ArrivalDateTime
        && string.Equals(left.DepartureIataCode ?? left.DepartureCity, right.DepartureIataCode ?? right.DepartureCity, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.ArrivalIataCode ?? left.ArrivalCity, right.ArrivalIataCode ?? right.ArrivalCity, StringComparison.OrdinalIgnoreCase);

    private async Task EnsureNameAvailable(string userId, string name, int? exceptId)
    {
        var taken = await _context.Travellers.AnyAsync(traveller =>
            traveller.OwnerUserId == userId
            && (exceptId == null || traveller.Id != exceptId)
            && traveller.Name.ToLower() == name.ToLower());
        if (taken)
        {
            throw new TravellerValidationException($"A traveller named \"{name}\" already exists.");
        }
    }

    private static string NormalizeName(string? name)
    {
        var normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new TravellerValidationException("Name is required.");
        }

        if (normalized.Length > Traveller.MaxNameLength)
        {
            throw new TravellerValidationException($"Name must be {Traveller.MaxNameLength} characters or fewer.");
        }

        return normalized;
    }

    private static string? NormalizeCountry(string? homeCountry)
    {
        var normalized = string.IsNullOrWhiteSpace(homeCountry) ? null : homeCountry.Trim();
        if (normalized is { Length: > Traveller.MaxHomeCountryLength })
        {
            throw new TravellerValidationException($"Home country must be {Traveller.MaxHomeCountryLength} characters or fewer.");
        }

        return normalized;
    }
}

public static class TripCopies
{
    public static Trip Onto(Trip source, Traveller traveller, string actorUserId) => new()
    {
        UserId = traveller.OwnerUserId,
        TravellerId = traveller.Id,
        CreatedByUserId = actorUserId,
        DepartureCountry = source.DepartureCountry,
        DepartureCity = source.DepartureCity,
        DepartureDateTime = source.DepartureDateTime,
        DepartureTimezone = source.DepartureTimezone,
        DepartureIataCode = source.DepartureIataCode,
        ArrivalCountry = source.ArrivalCountry,
        ArrivalCity = source.ArrivalCity,
        ArrivalDateTime = source.ArrivalDateTime,
        ArrivalTimezone = source.ArrivalTimezone,
        ArrivalIataCode = source.ArrivalIataCode,
        Source = source.Source,
        BookingRef = source.BookingRef,
        Notes = source.Notes
    };
}
