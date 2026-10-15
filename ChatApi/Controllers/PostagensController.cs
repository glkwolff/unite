using ChatApi.Data;
using ChatApi.Dtos;
using ChatApi.Models;
using ChatApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Controllers;

/// <summary>
/// Feed de noticias e botao "Ciente": qualquer autenticado le e marca ciente;
/// publicar, remover e ver quem marcou dependem do nivel hierarquico (regras
/// em <see cref="Permissoes"/>).
/// </summary>
[Authorize]
[ApiController]
[Route("api/postagens")]
public class PostagensController(AppDbContext db, Permissoes permissoes) : ControllerBase
{
    private const int LimitePadrao = 20;
    private const int LimiteMaximo = 50;

    /// <summary>
    /// Feed em ordem cronologica, da mais recente para a mais antiga.
    /// Paginacao por cursor: <paramref name="antes"/> e a data da ultima
    /// postagem que o front ja tem. Diferente de ?pagina=2, nao repete nem
    /// pula itens quando alguem publica enquanto o usuario rola a tela.
    /// </summary>
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

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PostagemDto>> Obter(Guid id)
    {
        var postagem = await BuscarComAutorAsync(id);
        if (postagem is null) return NotFound(new { erro = "Postagem nao encontrada." });

        return Ok((await MontarAsync([postagem]))[0]);
    }

    [HttpPost]
    public async Task<ActionResult<PostagemDto>> Publicar(PostagemEntradaDto dto)
    {
        if (!await permissoes.PodePublicarAsync())
            return Forbid();

        // Conferido separado para devolver o motivo: o supervisor pode publicar,
        // so nao pode marcar como institucional.
        if (dto.Institucional && !await permissoes.PodePublicarInstitucionalAsync())
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                erro = "Somente gerente ou diretor publicam avisos institucionais."
            });

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

        return CreatedAtAction(nameof(Obter), new { id = postagem.Id }, (await MontarAsync([postagem]))[0]);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(Guid id)
    {
        var postagem = await db.Postagens.FindAsync(id);
        if (postagem is null) return NotFound(new { erro = "Postagem nao encontrada." });

        if (!await permissoes.PodeRemoverPostagemAsync(postagem))
            return Forbid();

        // As ciencias da postagem vao junto (Cascade no AppDbContext).
        db.Postagens.Remove(postagem);
        await db.SaveChangesAsync();

        return NoContent();
    }

    // ---------------------------------------------------------------- ciente

    /// <summary>
    /// Marca "Ciente". Nao tem desfazer: e uma confirmacao de leitura, e quem
    /// cobra a leitura precisa poder confiar nela. Marcar de novo nao e erro —
    /// devolve a postagem como esta (duplo clique, duas abas abertas).
    /// </summary>
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
                // Dois cliques simultaneos: o indice unico barrou o segundo.
                // O resultado e o mesmo — a pessoa esta ciente.
            }
        }

        return Ok((await MontarAsync([postagem]))[0]);
    }

    /// <summary>
    /// Quem marcou "Ciente", na ordem em que marcou. Diretor ve tudo; gerente
    /// ve as postagens de supervisores e funcionarios (e as proprias);
    /// supervisor ve so as proprias, e nelas so os funcionarios.
    /// </summary>
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
                    MapeamentoOrganizacao.ParaResumo(c.Usuario),
                    ComoUtc(c.ConfirmadaEm)));
        }

        return Ok(visiveis);
    }

    // ------------------------------------------------------------ auxiliares

    private Task<Postagem?> BuscarComAutorAsync(Guid id) =>
        db.Postagens
            .Include(p => p.Autor).ThenInclude(a => a.Cargo)
            .FirstOrDefaultAsync(p => p.Id == id);

    /// <summary>
    /// Transforma postagens (com Autor.Cargo carregado) em DTOs. As ciencias de
    /// todas elas vem numa consulta so — sem isso cada pagina do feed faria
    /// uma consulta por postagem.
    /// </summary>
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
                ComoUtc(p.PublicadaEm),
                MapeamentoOrganizacao.ParaResumo(p.Autor),
                await permissoes.PodeRemoverPostagemAsync(p),
                minha is null ? null : ComoUtc(minha.ConfirmadaEm),
                permissoes.PodeMarcarCiente(p),
                podeVer,
                total));
        }

        return resultado;
    }

    /// <summary>
    /// O SQLite devolve DateTime sem fuso (Kind = Unspecified) e o JSON sairia
    /// sem o "Z" — o navegador leria como horario local e o aviso apareceria
    /// 3 horas adiantado. As datas sao gravadas em UTC, entao aqui so se declara isso.
    /// </summary>
    private static DateTime ComoUtc(DateTime data) => DateTime.SpecifyKind(data, DateTimeKind.Utc);
}
