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

        return await ConcluirEntradaAsync(usuario);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var usuario = await usuarios.FindByEmailAsync(dto.Email);
        if (usuario is null || !await usuarios.CheckPasswordAsync(usuario, dto.Senha))
            return Unauthorized(new { erro = "E-mail ou senha invalidos." });

        return await ConcluirEntradaAsync(usuario);
    }

    /// <summary>Endpoint protegido: valida o token e devolve o usuario logado.</summary>
    [Authorize]
    [HttpGet("eu")]
    public async Task<ActionResult<UsuarioDto>> Eu()
    {
        var usuario = await permissoes.UsuarioAtualAsync();
        return usuario is null ? Unauthorized() : Ok(UsuarioDto.De(usuario));
    }

    // ------------------------------------------------------------------- 2fa
    //
    // login ──senha ok──> 2FA ativo?          ── sim ──> desafio "codigo"     ──> /2fa/verificar
    //                     nivel exige 2FA?    ── sim ──> desafio "configurar" ──> /2fa/configurar + /2fa/ativar
    //                     senao               ────────> JWT
    //
    // Pelo perfil, quem ja esta logado ativa sem desafio: /2fa/configurar e
    // /2fa/ativar aceitam o JWT no lugar dele.

    /// <summary>Gera uma chave nova para o Authenticator (texto e URI do QR code).</summary>
    [HttpPost("2fa/configurar")]
    public async Task<ActionResult<ChaveAuthenticatorDto>> Configurar(DesafioRequisicaoDto dto)
    {
        var usuario = await DesafiadoOuLogadoAsync(dto.Desafio);
        if (usuario is null)
            return Unauthorized(new { erro = "Sessao expirada. Entre de novo." });
        if (usuario.TwoFactorEnabled)
            return Conflict(new { erro = "A verificacao em duas etapas ja esta ativa." });

        await usuarios.ResetAuthenticatorKeyAsync(usuario);
        var chave = (await usuarios.GetAuthenticatorKeyAsync(usuario))!;
        var uri = $"otpauth://totp/Unite:{Uri.EscapeDataString(usuario.Email!)}?secret={chave}&issuer=Unite&digits=6";

        return Ok(new ChaveAuthenticatorDto(chave, uri));
    }

    /// <summary>Confirma o primeiro codigo e liga o 2FA. Devolve o JWT.</summary>
    [HttpPost("2fa/ativar")]
    public async Task<ActionResult<AuthRespostaDto>> Ativar(CodigoDto dto)
    {
        var usuario = await DesafiadoOuLogadoAsync(dto.Desafio);
        if (usuario is null)
            return Unauthorized(new { erro = "Sessao expirada. Entre de novo." });
        if (usuario.TwoFactorEnabled)
            return Conflict(new { erro = "A verificacao em duas etapas ja esta ativa." });

        if (await ConferirCodigoAsync(usuario, dto.Codigo) is string erro)
            return Unauthorized(new { erro });

        await usuarios.SetTwoFactorEnabledAsync(usuario, true);
        return Ok(await MontarRespostaAsync(usuario));
    }

    /// <summary>Segunda etapa do login de quem ja usa o Authenticator.</summary>
    [HttpPost("2fa/verificar")]
    public async Task<ActionResult<AuthRespostaDto>> Verificar(CodigoDto dto)
    {
        var id = dto.Desafio is null ? null : await tokens.LerDesafioAsync(dto.Desafio);
        var usuario = id is null ? null : await usuarios.FindByIdAsync(id.Value.ToString());
        if (usuario is null || !usuario.TwoFactorEnabled)
            return Unauthorized(new { erro = "Sessao expirada. Entre de novo." });

        if (await ConferirCodigoAsync(usuario, dto.Codigo) is string erro)
            return Unauthorized(new { erro });

        return Ok(await MontarRespostaAsync(usuario));
    }

    /// <summary>Desliga o 2FA. Pede um codigo valido e so vale para quem o nivel nao obriga.</summary>
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

    /// <summary>Senha conferida: entrega o JWT ou, se falta o segundo fator, o desafio.</summary>
    private async Task<IActionResult> ConcluirEntradaAsync(Usuario usuario)
    {
        if (usuario.TwoFactorEnabled)
            return Ok(new DesafioDto(tokens.GerarDesafio(usuario), "codigo"));

        var nivel = await db.Cargos
            .Where(c => c.Id == usuario.CargoId)
            .Select(c => (NivelHierarquico?)c.Nivel)
            .FirstOrDefaultAsync();

        if (Permissoes.ExigeDoisFatores(nivel))
            return Ok(new DesafioDto(tokens.GerarDesafio(usuario), "configurar"));

        return Ok(await MontarRespostaAsync(usuario));
    }

    private async Task<Usuario?> DesafiadoOuLogadoAsync(string? desafio)
    {
        if (desafio is null) return await permissoes.UsuarioAtualAsync();

        var id = await tokens.LerDesafioAsync(desafio);
        return id is null ? null : await usuarios.FindByIdAsync(id.Value.ToString());
    }

    /// <summary>
    /// Nulo se o codigo bate. Cinco erros seguidos travam a conta por 5 minutos
    /// (lockout padrao do Identity): sao so 1 milhao de codigos possiveis.
    /// </summary>
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

    private async Task<AuthRespostaDto> MontarRespostaAsync(Usuario usuario)
    {
        // Recarrega com cargo e equipe para o token carregar o nivel hierarquico.
        var completo = await db.Users
            .Include(u => u.Cargo)
            .Include(u => u.Equipe)
            .FirstAsync(u => u.Id == usuario.Id);

        var (token, expiraEm) = tokens.Gerar(completo, completo.Cargo?.Nivel);
        return new AuthRespostaDto(token, expiraEm, UsuarioDto.De(completo));
    }
}
