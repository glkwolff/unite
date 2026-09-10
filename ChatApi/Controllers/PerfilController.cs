using ChatApi.Data;
using ChatApi.Dtos;
using ChatApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Controllers;

[Authorize]
[ApiController]
[Route("api/perfil")]
public class PerfilController(
    UserManager<Usuario> usuarios,
    AppDbContext db,
    IWebHostEnvironment ambiente) : ControllerBase
{
    // Guarda so as extensoes que o front realmente exibe como <img>.
    private static readonly string[] ExtensoesPermitidas = [".jpg", ".jpeg", ".png", ".webp"];
    private const long TamanhoMaximoBytes = 5 * 1024 * 1024; // 5 MB

    [HttpGet]
    public async Task<ActionResult<UsuarioDto>> Obter()
    {
        var usuario = await UsuarioAtualAsync();
        return usuario is null ? Unauthorized() : Ok(Mapear(usuario));
    }

    [HttpPut]
    public async Task<ActionResult<UsuarioDto>> Atualizar(AtualizarPerfilDto dto)
    {
        var usuario = await UsuarioAtualAsync();
        if (usuario is null) return Unauthorized();

        usuario.NomeCompleto = dto.NomeCompleto.Trim();
        var resultado = await usuarios.UpdateAsync(usuario);
        if (!resultado.Succeeded)
            return BadRequest(new { erro = string.Join(" ", resultado.Errors.Select(e => e.Description)) });

        return Ok(Mapear(usuario));
    }

    /// <summary>Recebe multipart/form-data com o campo "arquivo".</summary>
    [HttpPost("foto")]
    [RequestSizeLimit(TamanhoMaximoBytes)]
    public async Task<ActionResult<UsuarioDto>> EnviarFoto(IFormFile? arquivo)
    {
        if (arquivo is null || arquivo.Length == 0)
            return BadRequest(new { erro = "Envie um arquivo de imagem." });

        if (arquivo.Length > TamanhoMaximoBytes)
            return BadRequest(new { erro = "A imagem precisa ter ate 5 MB." });

        var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        if (!ExtensoesPermitidas.Contains(extensao))
            return BadRequest(new { erro = "Formatos aceitos: JPG, PNG ou WEBP." });

        var usuario = await UsuarioAtualAsync();
        if (usuario is null) return Unauthorized();

        var pastaWebroot = ambiente.WebRootPath
            ?? Path.Combine(ambiente.ContentRootPath, "wwwroot");
        var pastaFotos = Path.Combine(pastaWebroot, "uploads", "perfis");
        Directory.CreateDirectory(pastaFotos);

        // Nome fixo por usuario (nao pelo arquivo original): cada novo envio
        // substitui a foto anterior em vez de acumular lixo em disco.
        RemoverFotoAnteriorSeExistir(pastaFotos, usuario.Id);
        var nomeArquivo = $"{usuario.Id}{extensao}";
        var caminhoCompleto = Path.Combine(pastaFotos, nomeArquivo);

        await using (var destino = new FileStream(caminhoCompleto, FileMode.Create))
        {
            await arquivo.CopyToAsync(destino);
        }

        usuario.FotoUrl = $"/uploads/perfis/{nomeArquivo}";
        await usuarios.UpdateAsync(usuario);

        return Ok(Mapear(usuario));
    }

    [HttpDelete("foto")]
    public async Task<ActionResult<UsuarioDto>> RemoverFoto()
    {
        var usuario = await UsuarioAtualAsync();
        if (usuario is null) return Unauthorized();

        var pastaWebroot = ambiente.WebRootPath
            ?? Path.Combine(ambiente.ContentRootPath, "wwwroot");
        RemoverFotoAnteriorSeExistir(Path.Combine(pastaWebroot, "uploads", "perfis"), usuario.Id);

        usuario.FotoUrl = null;
        await usuarios.UpdateAsync(usuario);

        return Ok(Mapear(usuario));
    }

    private static void RemoverFotoAnteriorSeExistir(string pastaFotos, Guid usuarioId)
    {
        foreach (var extensao in ExtensoesPermitidas)
        {
            var caminho = Path.Combine(pastaFotos, $"{usuarioId}{extensao}");
            if (System.IO.File.Exists(caminho))
                System.IO.File.Delete(caminho);
        }
    }

    private async Task<Usuario?> UsuarioAtualAsync()
    {
        var id = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(id, out var usuarioId)) return null;

        return await db.Users
            .Include(u => u.Cargo)
            .Include(u => u.Equipe)
            .FirstOrDefaultAsync(u => u.Id == usuarioId);
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
