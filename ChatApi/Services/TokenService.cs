using System.Security.Claims;
using System.Text;
using ChatApi.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ChatApi.Services;

public class TokenService(IConfiguration config)
{
    public const string ClaimNivel = "nivel";

    /// <summary>
    /// O claim "nivel" e uma conveniencia para o front decidir o que desenhar.
    /// Autorizacao nao se apoia nele: <see cref="Permissoes"/> le o cargo do
    /// banco, senao uma promocao ou rebaixamento so valeria no proximo login.
    /// </summary>
    public (string Token, DateTime ExpiraEm) Gerar(Usuario usuario, NivelHierarquico? nivel)
    {
        var expiraEm = DateTime.UtcNow.AddHours(8);

        var claims = new List<Claim>
        {
            new("sub", usuario.Id.ToString()),
            new("email", usuario.Email ?? string.Empty),
            new("name", usuario.NomeCompleto)
        };

        if (nivel is not null)
            claims.Add(new Claim(ClaimNivel, nivel.Value.ToString()));

        return (Assinar(claims, Emissor, expiraEm), expiraEm);
    }

    /// <summary>
    /// Prova de que a senha ja foi conferida, valida por 5 minutos, para a
    /// etapa do codigo. Sai com outra audiencia ("unite-2fa"), entao o
    /// [Authorize] da API recusa este token: ele nao abre nenhuma rota.
    /// </summary>
    public string GerarDesafio(Usuario usuario) =>
        Assinar([new Claim("sub", usuario.Id.ToString())], AudienciaDesafio, DateTime.UtcNow.AddMinutes(5));

    /// <summary>Id do usuario do desafio, ou nulo se for invalido ou vencido.</summary>
    public async Task<Guid?> LerDesafioAsync(string desafio)
    {
        var resultado = await new JsonWebTokenHandler().ValidateTokenAsync(desafio, new TokenValidationParameters
        {
            ValidIssuer = Emissor,
            ValidAudience = AudienciaDesafio,
            IssuerSigningKey = Chave(),
            ClockSkew = TimeSpan.FromSeconds(30)
        });

        return resultado.IsValid
            && Guid.TryParse(resultado.ClaimsIdentity.FindFirst("sub")?.Value, out var id) ? id : null;
    }

    private string Emissor => config["Jwt:Emissor"] ?? "unite";
    private string AudienciaDesafio => $"{Emissor}-2fa";

    private SymmetricSecurityKey Chave() => new(Encoding.UTF8.GetBytes(
        config["Jwt:Chave"] ?? throw new InvalidOperationException("Jwt:Chave nao configurada.")));

    private string Assinar(List<Claim> claims, string audiencia, DateTime expiraEm) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Emissor,
            Audience = audiencia,
            Subject = new ClaimsIdentity(claims),
            Expires = expiraEm,
            SigningCredentials = new SigningCredentials(Chave(), SecurityAlgorithms.HmacSha256)
        });
}
