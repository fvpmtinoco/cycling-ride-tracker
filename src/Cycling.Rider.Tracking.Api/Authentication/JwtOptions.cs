namespace Cycling.Rider.Tracking.Api.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string Key { get; init; }
    public int ExpiryMinutes { get; init; } = 60;
}
