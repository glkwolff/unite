using System.Security.Claims;
using System.Text;
using ChatApi.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ChatApi.Services;

public class TokenService(IConfiguration config)
{
    public const string ClaimNivel = "nivel";

    public (string Token, DateTime ExpiraEm) Gerar(Usuario usuario, NivelHierarquico? nivel)
    {
        var chave = config["Jwt:Chave"]
            ?? throw new InvalidOperationException("Jwt:Chave nao configurada.");
        var emissor = config["Jwt:Emissor"] ?? "unite";
        var expiraEm = DateTime.UtcNow.AddHours(8);

        var claims = new List<Claim>
        {
            new("sub", usuario.Id.ToString()),
            new("email", usuario.Email ?? string.Empty),
            new("name", usuario.NomeCompleto)
        };

        if (nivel is not null)
            claims.Add(new Claim(ClaimNivel, nivel.Value.ToString()));

        var descritor = new SecurityTokenDescriptor
        {
            Issuer = emissor,
            Audience = emissor,
            Subject = new ClaimsIdentity(claims),
            Expires = expiraEm,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave)),
                SecurityAlgorithms.HmacSha256)
        };

        return (new JsonWebTokenHandler().CreateToken(descritor), expiraEm);
    }
}
