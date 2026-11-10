namespace ResidencyRoll.Api.Services;

public sealed class TravellerValidationException : Exception
{
    public TravellerValidationException(string message) : base(message)
    {
    }
}

public sealed class TravellerConflictException : Exception
{
    public TravellerConflictException(string message) : base(message)
    {
    }
}

public sealed class TravellerResolutionException : Exception
{
    public TravellerResolutionException(string message) : base(message)
    {
    }
}
