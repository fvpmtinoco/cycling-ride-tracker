using Cycling.Rider.Tracking.Api.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Cycling.Rider.Tracking.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(IOptions<AuthCredentialsOptions> credentials, JwtTokenGenerator tokenGenerator) : ControllerBase
{
    public sealed record TokenRequest(string Username, string Password);
    public sealed record TokenResponse(string AccessToken);

    [HttpPost("token")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IResult Token(TokenRequest request)
    {
        AuthCredentialsOptions expected = credentials.Value;

        if (request.Username != expected.Username || request.Password != expected.Password)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new TokenResponse(tokenGenerator.Generate(request.Username)));
    }
}
