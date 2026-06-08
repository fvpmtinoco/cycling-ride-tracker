namespace Cycling.Rider.Tracking.Domain.Idempotency;

public sealed class IdempotencyKey
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Key { get; init; }
    public required string RequestHash { get; init; }
    public required int ResponseStatusCode { get; init; }
    public required string ResponseBody { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; set; }
}
