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
    TokenService tokens) : ControllerBase
{
    [HttpPost("registrar")]
    public async Task<ActionResult<AuthRespostaDto>> Registrar(RegistrarDto dto)
    {
        if (await usuarios.FindByEmailAsync(dto.Email) is not null)
            return Conflict(new { erro = "Ja existe um usuario com este e-mail." });

        var usuario = new Usuario
        {
            UserName = dto.Email,
            Email = dto.Email,
            NomeCompleto = dto.NomeCompleto
        };

        var resultado = await usuarios.CreateAsync(usuario, dto.Senha);
        if (!resultado.Succeeded)
            return BadRequest(new { erro = string.Join(" ", resultado.Errors.Select(e => e.Description)) });

        return Ok(await MontarRespostaAsync(usuario));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthRespostaDto>> Login(LoginDto dto)
    {
        var usuario = await usuarios.FindByEmailAsync(dto.Email);
        if (usuario is null || !await usuarios.CheckPasswordAsync(usuario, dto.Senha))
            return Unauthorized(new { erro = "E-mail ou senha invalidos." });

        return Ok(await MontarRespostaAsync(usuario));
    }

    /// <summary>Endpoint protegido: valida o token e devolve o usuario logado.</summary>
    [Authorize]
    [HttpGet("eu")]
    public async Task<ActionResult<UsuarioDto>> Eu()
    {
        var id = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(id, out var usuarioId))
            return Unauthorized();

        var usuario = await db.Users
            .Include(u => u.Cargo)
            .Include(u => u.Equipe)
            .FirstOrDefaultAsync(u => u.Id == usuarioId);

        return usuario is null ? Unauthorized() : Ok(Mapear(usuario));
    }

    private async Task<AuthRespostaDto> MontarRespostaAsync(Usuario usuario)
    {
        // Recarrega com cargo e equipe para o token carregar o nivel hierarquico.
        var completo = await db.Users
            .Include(u => u.Cargo)
            .Include(u => u.Equipe)
            .FirstAsync(u => u.Id == usuario.Id);

        var (token, expiraEm) = tokens.Gerar(completo, completo.Cargo?.Nivel);
        return new AuthRespostaDto(token, expiraEm, Mapear(completo));
    }

    private static UsuarioDto Mapear(Usuario u) => new(
        u.Id,
        u.Email ?? string.Empty,
        u.NomeCompleto,
        u.FotoUrl,
        u.Cargo?.Nome,
        u.Equipe?.Nome,
        u.Cargo?.Nivel);
}
