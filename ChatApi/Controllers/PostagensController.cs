using ChatApi.Data;
using ChatApi.Dtos;
using ChatApi.Models;
using ChatApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Controllers;

/// <summary>Feed de noticias e botao "Ciente". As regras de quem pode o que estao em Permissoes.</summary>
[Authorize]
[ApiController]
[Route("api/postagens")]
public class PostagensController(AppDbContext db, Permissoes permissoes) : ControllerBase
{
    private const int LimitePadrao = 20;
    private const int LimiteMaximo = 50;

    /// <summary>Feed do mais novo ao mais antigo. Proxima pagina: ?antes= a data da ultima recebida.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginaFeedDto>> Listar(
        [FromQuery] DateTimeOffset? antes,
        [FromQuery] int limite = LimitePadrao)
    {
        limite = Math.Clamp(limite, 1, LimiteMaximo);

        var consulta = db.Postagens
            .Include(p => p.Autor).ThenInclude(a => a.Cargo)
            .AsQueryable();

        if (antes is DateTimeOffset cursor)
        {
            var limiteUtc = cursor.UtcDateTime;
            consulta = consulta.Where(p => p.PublicadaEm < limiteUtc);
        }

        // Busca um a mais so para saber se existe proxima pagina.
        var postagens = await consulta
            .OrderByDescending(p => p.PublicadaEm)
            .ThenByDescending(p => p.Id)
            .Take(limite + 1)
            .ToListAsync();

        var temMais = postagens.Count > limite;
        var pagina = postagens.Take(limite).ToList();

        return Ok(new PaginaFeedDto(await MontarAsync(pagina), temMais));
    }

    [HttpPost]
    public async Task<ActionResult<PostagemDto>> Publicar(PostagemEntradaDto dto)
    {
        if (!await permissoes.PodePublicarAsync())
            return Forbid();

        if (dto.Institucional && !await permissoes.PodePublicarInstitucionalAsync())
            return Forbid();

        var autor = await permissoes.UsuarioAtualAsync();
        if (autor is null) return Unauthorized();

        var postagem = new Postagem
        {
            AutorId = autor.Id,
            Autor = autor,
            Titulo = dto.Titulo.Trim(),
            Conteudo = dto.Conteudo.Trim(),
            Institucional = dto.Institucional,
            PublicadaEm = DateTime.UtcNow
        };

        db.Postagens.Add(postagem);
        await db.SaveChangesAsync();

        return Ok((await MontarAsync([postagem]))[0]);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(Guid id)
    {
        var postagem = await db.Postagens.FindAsync(id);
        if (postagem is null) return NotFound(new { erro = "Postagem nao encontrada." });

        if (!await permissoes.PodeRemoverPostagemAsync(postagem))
            return Forbid();

        db.Postagens.Remove(postagem);
        await db.SaveChangesAsync();

        return NoContent();
    }

    // ---- ciente

    /// <summary>Marca "Ciente" (nao tem desfazer). Marcar de novo nao e erro.</summary>
    [HttpPost("{id:guid}/ciente")]
    public async Task<ActionResult<PostagemDto>> MarcarCiente(Guid id)
    {
        var postagem = await BuscarComAutorAsync(id);
        if (postagem is null) return NotFound(new { erro = "Postagem nao encontrada." });

        if (!permissoes.PodeMarcarCiente(postagem))
            return BadRequest(new { erro = "O autor nao marca ciente na propria postagem." });

        if (permissoes.IdLogado is not Guid usuarioId) return Unauthorized();

        var jaMarcou = await db.Ciencias.AnyAsync(c => c.PostagemId == id && c.UsuarioId == usuarioId);
        if (!jaMarcou)
        {
            db.Ciencias.Add(new Ciencia { PostagemId = id, UsuarioId = usuarioId, ConfirmadaEm = DateTime.UtcNow });
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Dois cliques ao mesmo tempo: a chave barrou o segundo, a pessoa ja esta ciente.
            }
        }

        return Ok((await MontarAsync([postagem]))[0]);
    }

    /// <summary>Quem marcou "Ciente" e quando (quem ve o que: Permissoes).</summary>
    [HttpGet("{id:guid}/ciencias")]
    public async Task<ActionResult<IEnumerable<CienciaDto>>> ListarCiencias(Guid id)
    {
        var postagem = await BuscarComAutorAsync(id);
        if (postagem is null) return NotFound(new { erro = "Postagem nao encontrada." });

        if (!await permissoes.PodeVerCienciasAsync(postagem))
            return Forbid();

        var ciencias = await db.Ciencias
            .Include(c => c.Usuario).ThenInclude(u => u.Cargo)
            .Where(c => c.PostagemId == id)
            .OrderBy(c => c.ConfirmadaEm)
            .ToListAsync();

        var visiveis = new List<CienciaDto>();
        foreach (var c in ciencias)
        {
            if (await permissoes.PodeVerLeitorAsync(c.Usuario.Cargo?.Nivel))
                visiveis.Add(new CienciaDto(
                    MembroResumoDto.De(c.Usuario),
                    c.ConfirmadaEm));
        }

        return Ok(visiveis);
    }

    // ---- apoio

    private Task<Postagem?> BuscarComAutorAsync(Guid id) =>
        db.Postagens
            .Include(p => p.Autor).ThenInclude(a => a.Cargo)
            .FirstOrDefaultAsync(p => p.Id == id);

    /// <summary>Postagens (com Autor.Cargo) viram DTO; as ciencias de todas vem numa consulta so.</summary>
    private async Task<List<PostagemDto>> MontarAsync(IReadOnlyList<Postagem> postagens)
    {
        var ids = postagens.Select(p => p.Id).ToList();
        var usuarioId = permissoes.IdLogado;

        var ciencias = await db.Ciencias
            .Where(c => ids.Contains(c.PostagemId))
            .Select(c => new
            {
                c.PostagemId,
                c.UsuarioId,
                NivelLeitor = c.Usuario.Cargo == null ? (NivelHierarquico?)null : c.Usuario.Cargo.Nivel,
                c.ConfirmadaEm
            })
            .ToListAsync();

        var porPostagem = ciencias.ToLookup(c => c.PostagemId);
        var resultado = new List<PostagemDto>(postagens.Count);

        foreach (var p in postagens)
        {
            var daPostagem = porPostagem[p.Id].ToList();
            var minha = daPostagem.FirstOrDefault(c => c.UsuarioId == usuarioId);
            var podeVer = await permissoes.PodeVerCienciasAsync(p);

            int? total = null;
            if (podeVer)
            {
                total = 0;
                foreach (var c in daPostagem)
                    if (await permissoes.PodeVerLeitorAsync(c.NivelLeitor)) total++;
            }

            resultado.Add(new PostagemDto(
                p.Id,
                p.Titulo,
                p.Conteudo,
                p.Institucional,
                p.PublicadaEm,
                MembroResumoDto.De(p.Autor),
                await permissoes.PodeRemoverPostagemAsync(p),
                minha is null ? null : minha.ConfirmadaEm,
                permissoes.PodeMarcarCiente(p),
                podeVer,
                total));
        }

        return resultado;
    }
}
