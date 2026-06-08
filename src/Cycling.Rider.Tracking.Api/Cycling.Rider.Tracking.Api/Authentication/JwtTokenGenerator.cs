using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Cycling.Rider.Tracking.Api.Authentication;

public sealed class JwtTokenGenerator(IOptions<JwtOptions> options)
{
    public string Generate(string username)
    {
        JwtOptions jwt = options.Value;
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.Name, username)
            ]),
            Expires = DateTime.UtcNow.AddMinutes(jwt.ExpiryMinutes),
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
