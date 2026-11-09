using System.Threading.RateLimiting;

namespace ResidencyRoll.Api.Mcp;

/// <summary>
/// Tighter limit for parse_itinerary, which spends OpenAI tokens. The /mcp endpoint has its own per-user limit.
/// </summary>
public sealed class ItineraryParseLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create<string, string>(userId =>
        RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    public async Task EnsureAllowedAsync(string userId, CancellationToken cancellationToken)
    {
        using var lease = await _limiter.AcquireAsync(userId, 1, cancellationToken);
        if (!lease.IsAcquired)
        {
            throw new ModelContextProtocol.McpException("parse_itinerary is rate limited. Try again in a minute.");
        }
    }

    public void Dispose() => _limiter.Dispose();
}
