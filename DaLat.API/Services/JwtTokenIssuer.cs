using System.Security.Claims;
using System.Text;
using DaLat.BLL.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DaLat.API.Services;

public class JwtTokenIssuer(IOptions<JwtOptions> options) : IJwtTokenIssuer
{
    public (string Token, DateTime ExpiresAt) Issue(int userId, string username, string displayName)
    {
        var o = options.Value;
        var expires = DateTime.UtcNow.AddMinutes(o.ExpireMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", userId.ToString()),
                new Claim("name", username),
                new Claim("display_name", displayName ?? username),
                new Claim("role", "admin")
            ]),
            Issuer = o.Issuer,
            Audience = o.Audience,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key)), SecurityAlgorithms.HmacSha256)
        };
        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}
