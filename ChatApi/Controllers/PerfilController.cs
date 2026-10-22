using ChatApi.Dtos;
using ChatApi.Models;
using ChatApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ChatApi.Controllers;

[Authorize]
[ApiController]
[Route("api/perfil")]
public class PerfilController(
    UserManager<Usuario> usuarios,
    Permissoes permissoes,
    IWebHostEnvironment ambiente) : ControllerBase
{
    // Guarda so as extensoes que o front realmente exibe como <img>.
    private static readonly string[] ExtensoesPermitidas = [".jpg", ".jpeg", ".png", ".webp"];
    private const long TamanhoMaximoBytes = 5 * 1024 * 1024; // 5 MB

    [HttpPut]
    public async Task<ActionResult<UsuarioDto>> Atualizar(AtualizarPerfilDto dto)
    {
        var usuario = await permissoes.UsuarioAtualAsync();
        if (usuario is null) return Unauthorized();

        usuario.NomeCompleto = dto.NomeCompleto.Trim();
        var resultado = await usuarios.UpdateAsync(usuario);
        if (!resultado.Succeeded)
            return BadRequest(new { erro = string.Join(" ", resultado.Errors.Select(e => e.Description)) });

        return Ok(UsuarioDto.De(usuario));
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

        var usuario = await permissoes.UsuarioAtualAsync();
        if (usuario is null) return Unauthorized();

        var pastaWebroot = ambiente.WebRootPath
            ?? Path.Combine(ambiente.ContentRootPath, "wwwroot");
        var pastaFotos = Path.Combine(pastaWebroot, "uploads", "perfis");
        Directory.CreateDirectory(pastaFotos);

        // Nome fixo por usuario: a foto nova substitui a anterior.
        RemoverFotoAnteriorSeExistir(pastaFotos, usuario.Id);
        var nomeArquivo = $"{usuario.Id}{extensao}";
        var caminhoCompleto = Path.Combine(pastaFotos, nomeArquivo);

        await using (var destino = new FileStream(caminhoCompleto, FileMode.Create))
        {
            await arquivo.CopyToAsync(destino);
        }

        usuario.FotoUrl = $"/uploads/perfis/{nomeArquivo}";
        await usuarios.UpdateAsync(usuario);

        return Ok(UsuarioDto.De(usuario));
    }

    [HttpDelete("foto")]
    public async Task<ActionResult<UsuarioDto>> RemoverFoto()
    {
        var usuario = await permissoes.UsuarioAtualAsync();
        if (usuario is null) return Unauthorized();

        var pastaWebroot = ambiente.WebRootPath
            ?? Path.Combine(ambiente.ContentRootPath, "wwwroot");
        RemoverFotoAnteriorSeExistir(Path.Combine(pastaWebroot, "uploads", "perfis"), usuario.Id);

        usuario.FotoUrl = null;
        await usuarios.UpdateAsync(usuario);

        return Ok(UsuarioDto.De(usuario));
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

}
