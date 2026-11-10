namespace ResidencyRoll.Api.Services;

public static class TripText
{
    public static bool TryOptional(string? value, int maxLength, string name, out string? stored, out string? error)
    {
        stored = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            error = $"{name} must be {maxLength} characters or fewer.";
            return false;
        }

        stored = trimmed;
        return true;
    }
}
