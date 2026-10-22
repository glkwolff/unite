using ChatApi.Data;
using ChatApi.Dtos;
using ChatApi.Models;
using ChatApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<Usuario> usuarios,
    AppDbContext db,
    TokenService tokens,
    Permissoes permissoes) : ControllerBase
{
    [HttpPost("registrar")]
    public async Task<IActionResult> Registrar(RegistrarDto dto)
    {
        if (await usuarios.FindByEmailAsync(dto.Email) is not null)
            return Conflict(new { erro = "Ja existe um usuario com este e-mail." });

        // Ovo e galinha: sem diretor ninguem atribui cargo, e sem cargo
        // ninguem e diretor. Quem registra a conta numa base vazia assume a
        // diretoria. A corrida aqui e aceitavel — instancia unica, e o caso e
        // o primeirissimo registro do sistema.
        var baseVazia = !await db.Users.AnyAsync();

        var usuario = new Usuario
        {
            UserName = dto.Email,
            Email = dto.Email,
            NomeCompleto = dto.NomeCompleto
        };

        var resultado = await usuarios.CreateAsync(usuario, dto.Senha);
        if (!resultado.Succeeded)
            return BadRequest(new { erro = string.Join(" ", resultado.Errors.Select(e => e.Description)) });

        if (baseVazia)
        {
            usuario.CargoId = await db.Cargos
                .Where(c => c.Nivel == NivelHierarquico.Diretor)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync();
            await db.SaveChangesAsync();
        }

        return NoContent();
    }

    /// <summary>Senha e, quando o 2FA vale para a pessoa, o codigo do Authenticator.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var usuario = await usuarios.FindByEmailAsync(dto.Email);
        if (usuario is null || !await usuarios.CheckPasswordAsync(usuario, dto.Senha))
            return Unauthorized(new { erro = "E-mail ou senha invalidos." });

        await db.Entry(usuario).Reference(u => u.Cargo).LoadAsync();
        await db.Entry(usuario).Reference(u => u.Equipe).LoadAsync();

        var precisaCodigo = usuario.TwoFactorEnabled || Permissoes.ExigeDoisFatores(usuario.Cargo?.Nivel);
        if (!precisaCodigo)
            return Ok(MontarResposta(usuario));

        if (string.IsNullOrWhiteSpace(dto.Codigo))
        {
            if (usuario.TwoFactorEnabled)
                return Ok(new SegundaEtapaDto("codigo", null, null));

            // Cargo exige e ainda nao configurou: devolve o QR code
            var (chave, uri) = await ChaveAsync(usuario);
            return Ok(new SegundaEtapaDto("configurar", chave, uri));
        }

        if (await ConferirCodigoAsync(usuario, dto.Codigo) is string erro)
            return Unauthorized(new { erro });

        if (!usuario.TwoFactorEnabled)
            await usuarios.SetTwoFactorEnabledAsync(usuario, true);

        return Ok(MontarResposta(usuario));
    }

    [Authorize]
    [HttpGet("eu")]
    public async Task<ActionResult<UsuarioDto>> Eu()
    {
        var usuario = await permissoes.UsuarioAtualAsync();
        return usuario is null ? Unauthorized() : Ok(UsuarioDto.De(usuario));
    }

    // ---- 2fa pelo perfil (quem ja esta logado)

    [Authorize]
    [HttpPost("2fa/configurar")]
    public async Task<ActionResult<ChaveAuthenticatorDto>> Configurar()
    {
        var usuario = await permissoes.UsuarioAtualAsync();
        if (usuario is null) return Unauthorized();
        if (usuario.TwoFactorEnabled)
            return Conflict(new { erro = "A verificacao em duas etapas ja esta ativa." });

        var (chave, uri) = await ChaveAsync(usuario);
        return Ok(new ChaveAuthenticatorDto(chave, uri));
    }

    [Authorize]
    [HttpPost("2fa/ativar")]
    public async Task<ActionResult<UsuarioDto>> Ativar(CodigoDto dto)
    {
        var usuario = await permissoes.UsuarioAtualAsync();
        if (usuario is null) return Unauthorized();
        if (usuario.TwoFactorEnabled)
            return Conflict(new { erro = "A verificacao em duas etapas ja esta ativa." });

        if (await ConferirCodigoAsync(usuario, dto.Codigo) is string erro)
            return Unauthorized(new { erro });

        await usuarios.SetTwoFactorEnabledAsync(usuario, true);
        return Ok(UsuarioDto.De(usuario));
    }

    [Authorize]
    [HttpPost("2fa/desativar")]
    public async Task<ActionResult<UsuarioDto>> Desativar(CodigoDto dto)
    {
        var usuario = await permissoes.UsuarioAtualAsync();
        if (usuario is null) return Unauthorized();
        if (!usuario.TwoFactorEnabled)
            return Conflict(new { erro = "A verificacao em duas etapas ja esta desligada." });
        if (Permissoes.ExigeDoisFatores(usuario.Cargo?.Nivel))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { erro = "Seu cargo exige a verificacao em duas etapas." });

        if (await ConferirCodigoAsync(usuario, dto.Codigo) is string erro)
            return Unauthorized(new { erro });

        await usuarios.SetTwoFactorEnabledAsync(usuario, false);
        await usuarios.ResetAuthenticatorKeyAsync(usuario);
        return Ok(UsuarioDto.De(usuario));
    }

    // ---- apoio

    /// <summary>So gera chave se ainda nao existe: o QR code ja lido continua valendo.</summary>
    private async Task<(string Chave, string Uri)> ChaveAsync(Usuario usuario)
    {
        var chave = await usuarios.GetAuthenticatorKeyAsync(usuario);
        if (chave is null)
        {
            await usuarios.ResetAuthenticatorKeyAsync(usuario);
            chave = (await usuarios.GetAuthenticatorKeyAsync(usuario))!;
        }

        var uri = $"otpauth://totp/Unite:{Uri.EscapeDataString(usuario.Email!)}?secret={chave}&issuer=Unite&digits=6";
        return (chave, uri);
    }

    /// <summary>Nulo se o codigo bate. 5 erros seguidos travam a conta por 5 min (lockout do Identity).</summary>
    private async Task<string?> ConferirCodigoAsync(Usuario usuario, string codigo)
    {
        if (await usuarios.IsLockedOutAsync(usuario))
            return "Muitas tentativas erradas. Tente de novo em alguns minutos.";

        var valido = await usuarios.VerifyTwoFactorTokenAsync(
            usuario, TokenOptions.DefaultAuthenticatorProvider, codigo.Replace(" ", ""));

        if (!valido)
        {
            await usuarios.AccessFailedAsync(usuario);
            return "Codigo invalido.";
        }

        await usuarios.ResetAccessFailedCountAsync(usuario);
        return null;
    }

    /// <summary>Exige Cargo e Equipe carregados.</summary>
    private AuthRespostaDto MontarResposta(Usuario usuario)
    {
        var (token, expiraEm) = tokens.Gerar(usuario, usuario.Cargo?.Nivel);
        return new AuthRespostaDto(token, expiraEm, UsuarioDto.De(usuario));
    }
}
