using ChatApi.Data;
using ChatApi.Dtos;
using ChatApi.Hubs;
using ChatApi.Models;
using ChatApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Controllers;

/// <summary>
/// Conversas privadas. Nao pergunta nada a <see cref="Permissoes"/> de
/// proposito: conversa nao segue hierarquia — qualquer pessoa fala com
/// qualquer pessoa. A unica trava e ser participante da sala.
/// </summary>
[Authorize]
[ApiController]
[Route("api/salas")]
public class SalasController(AppDbContext db, Permissoes permissoes, IHubContext<ChatHub> hub)
    : ControllerBase
{
    /// <summary>Minhas conversas, a mais recente primeiro.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SalaResumoDto>>> Listar()
    {
        var meuId = permissoes.IdLogado ?? Guid.Empty;

        var salas = await db.Salas
            .Where(s => s.Tipo == TipoSala.Privada && s.Participantes.Any(p => p.UsuarioId == meuId))
            .Include(s => s.Participantes).ThenInclude(p => p.Usuario).ThenInclude(u => u!.Cargo)
            .ToListAsync();

        var ultimas = await UltimasMensagensAsync(salas.Select(s => s.Id).ToList());

        return Ok(salas
            .Select(s => Mapear(s, meuId, ultimas.GetValueOrDefault(s.Id)))
            .Where(r => r is not null)
            .OrderByDescending(r => r!.UltimaEm ?? DateTime.MinValue));
    }

    /// <summary>
    /// Abre a conversa com alguem. E idempotente: se as duas pessoas ja
    /// conversaram antes, devolve a mesma sala em vez de criar outra.
    /// </summary>
    [HttpPost("privada")]
    public async Task<ActionResult<SalaResumoDto>> AbrirPrivada(AbrirPrivadaDto dto)
    {
        var meuId = permissoes.IdLogado ?? Guid.Empty;
        if (dto.UsuarioId == meuId)
            return BadRequest(new { erro = "Nao da para abrir uma conversa com voce mesmo." });

        if (!await db.Users.AnyAsync(u => u.Id == dto.UsuarioId))
            return NotFound(new { erro = "Usuario nao encontrado." });

        var sala = await db.Salas
            .Where(s => s.Tipo == TipoSala.Privada
                     && s.Participantes.Any(p => p.UsuarioId == meuId)
                     && s.Participantes.Any(p => p.UsuarioId == dto.UsuarioId))
            .FirstOrDefaultAsync();

        if (sala is null)
        {
            sala = new Sala
            {
                Tipo = TipoSala.Privada,
                Participantes =
                [
                    new SalaUsuario { UsuarioId = meuId },
                    new SalaUsuario { UsuarioId = dto.UsuarioId }
                ]
            };
            db.Salas.Add(sala);
            await db.SaveChangesAsync();
        }

        await db.Entry(sala).Collection(s => s.Participantes).Query()
            .Include(p => p.Usuario).ThenInclude(u => u!.Cargo).LoadAsync();

        var ultimas = await UltimasMensagensAsync([sala.Id]);

        return Ok(Mapear(sala, meuId, ultimas.GetValueOrDefault(sala.Id)));
    }

    /// <summary>
    /// Historico da sala, em ordem cronologica. O limite pega as mais recentes
    /// e devolve na ordem de leitura — a paginacao completa fica para 08/10.
    /// </summary>
    [HttpGet("{id:guid}/mensagens")]
    public async Task<ActionResult<IEnumerable<MensagemDto>>> Mensagens(Guid id, int limite = 50)
    {
        if (await NegarSeNaoParticipoAsync(id) is ObjectResult negado) return negado;

        var mensagens = await db.Mensagens
            .Where(m => m.SalaId == id)
            .Include(m => m.Autor)
            .OrderByDescending(m => m.EnviadaEm)
            .Take(Math.Clamp(limite, 1, 200))
            .ToListAsync();

        mensagens.Reverse();

        return Ok(mensagens.Select(Mapear));
    }

    /// <summary>
    /// Grava a mensagem e avisa os participantes pelo hub. O autor tambem
    /// recebe o evento: assim as outras abas dele ficam em dia.
    /// </summary>
    [HttpPost("{id:guid}/mensagens")]
    public async Task<ActionResult<MensagemDto>> Enviar(Guid id, EnviarMensagemDto dto)
    {
        if (await NegarSeNaoParticipoAsync(id) is ObjectResult negado) return negado;

        var texto = dto.Texto.Trim();
        if (texto.Length == 0) return BadRequest(new { erro = "A mensagem nao pode ficar vazia." });

        var mensagem = new Mensagem
        {
            SalaId = id,
            AutorId = permissoes.IdLogado ?? Guid.Empty,
            Texto = texto
        };

        db.Mensagens.Add(mensagem);
        await db.SaveChangesAsync();

        await db.Entry(mensagem).Reference(m => m.Autor).LoadAsync();
        var mapeada = Mapear(mensagem);

        var destinatarios = await db.SalaUsuarios
            .Where(p => p.SalaId == id)
            .Select(p => p.UsuarioId)
            .ToListAsync();

        // O ToString() fica no C# de proposito: traduzido para SQL, o SQLite
        // devolveria o GUID em maiusculas, e o Clients.Users compara com o
        // "sub" do token (minusculo) caractere a caractere — ninguem receberia.
        await hub.Clients
            .Users(destinatarios.Select(d => d.ToString()).ToList())
            .SendAsync("MensagemRecebida", mapeada);

        return Ok(mapeada);
    }

    // ------------------------------------------------------------- apoio

    /// <summary>403 para quem nao esta na sala; 404 para sala inexistente.</summary>
    private async Task<ObjectResult?> NegarSeNaoParticipoAsync(Guid salaId)
    {
        if (!await db.Salas.AnyAsync(s => s.Id == salaId))
            return NotFound(new { erro = "Sala nao encontrada." });

        var meuId = permissoes.IdLogado ?? Guid.Empty;
        if (!await db.SalaUsuarios.AnyAsync(p => p.SalaId == salaId && p.UsuarioId == meuId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                erro = "Voce nao participa desta conversa."
            });
        }

        return null;
    }

    /// <summary>
    /// Preview de cada sala numa consulta so, sem N+1 na listagem. E uma
    /// subconsulta correlacionada porque GroupBy projetando a linha inteira
    /// nao tem traducao para SQL no EF Core.
    /// </summary>
    private async Task<Dictionary<Guid, Mensagem>> UltimasMensagensAsync(List<Guid> salaIds)
    {
        var ultimas = await db.Salas
            .Where(s => salaIds.Contains(s.Id))
            .Select(s => s.Mensagens.OrderByDescending(m => m.EnviadaEm).FirstOrDefault())
            .ToListAsync();

        return ultimas.Where(m => m is not null).ToDictionary(m => m!.SalaId, m => m!);
    }

    private static SalaResumoDto? Mapear(Sala sala, Guid meuId, Mensagem? ultima)
    {
        var outro = sala.Participantes.FirstOrDefault(p => p.UsuarioId != meuId)?.Usuario;
        // Sala privada sem o outro lado (conta removida) nao tem o que mostrar.
        if (outro is null) return null;

        return new SalaResumoDto(
            sala.Id,
            MapeamentoOrganizacao.ParaResumo(outro),
            ultima?.Texto,
            ultima?.EnviadaEm);
    }

    private static MensagemDto Mapear(Mensagem m) => new(
        m.Id,
        m.SalaId,
        m.AutorId,
        m.Autor?.NomeCompleto ?? string.Empty,
        m.Texto,
        m.EnviadaEm);
}
