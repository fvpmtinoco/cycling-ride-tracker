namespace Cycling.Rider.Tracking.Api.Authentication;

public sealed class AuthCredentialsOptions
{
    public const string SectionName = "Auth";

    public required string Username { get; init; }
    public required string Password { get; init; }
}
