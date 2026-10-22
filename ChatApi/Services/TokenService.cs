using System.Security.Claims;
using System.Text;
using ChatApi.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ChatApi.Services;

public class TokenService(IConfiguration config)
{
    /// <summary>JWT de 8 h so com id, e-mail e nome. O nivel nao vai no token: e lido do banco.</summary>
    public (string Token, DateTime ExpiraEm) Gerar(Usuario usuario)
    {
        var chave = config["Jwt:Chave"]
            ?? throw new InvalidOperationException("Jwt:Chave nao configurada.");
        var emissor = config["Jwt:Emissor"] ?? "unite";
        var expiraEm = DateTime.UtcNow.AddHours(8);

        Claim[] claims =
        [
            new("sub", usuario.Id.ToString()),
            new("email", usuario.Email ?? string.Empty),
            new("name", usuario.NomeCompleto)
        ];

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
