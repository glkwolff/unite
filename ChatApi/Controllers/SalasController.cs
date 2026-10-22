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
/// As conversas: a privada, entre duas pessoas, e o canal da equipe. Nao
/// pergunta a <see cref="Permissoes"/> se alguem "pode" — conversa nao segue
/// hierarquia, e a unica trava e participar da sala. A Permissoes entra aqui
/// so para responder qual e a minha equipe.
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
        var meuId = MeuId;
        var minhaEquipe = (await permissoes.UsuarioAtualAsync())?.EquipeId;

        var privadas = await db.Salas
            .Where(s => s.Tipo == TipoSala.Privada && s.Participantes.Any(p => p.UsuarioId == meuId))
            .Include(s => s.Participantes).ThenInclude(p => p.Usuario).ThenInclude(u => u!.Cargo)
            .ToListAsync();

        // Sem equipe nao ha canal para listar.
        var canais = minhaEquipe is Guid equipeId
            ? await db.Salas
                .Where(s => s.Tipo == TipoSala.Grupo && s.EquipeId == equipeId)
                .Include(s => s.Equipe)
                .ToListAsync()
            : [];

        var ultimas = await UltimasMensagensAsync(
            privadas.Concat(canais).Select(s => s.Id).ToList());

        var resumos = privadas
            .Select(s => SalaResumoDto.Privada(s, meuId, ultimas.GetValueOrDefault(s.Id)))
            .Where(r => r is not null)
            .Concat(canais.Select(s => SalaResumoDto.Canal(s, ultimas.GetValueOrDefault(s.Id))));

        return Ok(resumos.OrderByDescending(r => r!.UltimaEm ?? DateTime.MinValue));
    }

    /// <summary>
    /// Abre a conversa com alguem. E idempotente: se as duas pessoas ja
    /// conversaram antes, devolve a mesma sala em vez de criar outra.
    /// </summary>
    [HttpPost("privada")]
    public async Task<ActionResult<SalaResumoDto>> AbrirPrivada(AbrirPrivadaDto dto)
    {
        var meuId = MeuId;
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

        return Ok(SalaResumoDto.Privada(sala, meuId, ultimas.GetValueOrDefault(sala.Id)));
    }

    /// <summary>
    /// Abre o canal da MINHA equipe, e so dela: a equipe sai do usuario logado
    /// em vez de vir no corpo, entao ninguem abre o canal de uma equipe que nao
    /// e a sua. E idempotente igual ao <see cref="AbrirPrivada"/>.
    ///
    /// O canal nasce no primeiro clique, e nao junto com a equipe, porque as
    /// equipes que ja estao no banco nunca ganhariam canal — precisaria de um
    /// backfill. E nasce num POST, nao dentro do GET da listagem, porque criar
    /// registro num GET e efeito colateral escondido.
    /// </summary>
    [HttpPost("equipe")]
    public async Task<ActionResult<SalaResumoDto>> AbrirCanalDaEquipe()
    {
        var usuario = await permissoes.UsuarioAtualAsync();
        if (usuario?.EquipeId is not Guid equipeId)
            return BadRequest(new { erro = "Voce nao esta em nenhuma equipe." });

        var sala = await db.Salas
            .Where(s => s.Tipo == TipoSala.Grupo && s.EquipeId == equipeId)
            .Include(s => s.Equipe)
            .FirstOrDefaultAsync();

        if (sala is null)
        {
            // Sem linha nenhuma em SalaUsuarios e com Nome vazio: participantes
            // e titulo saem da equipe na hora da leitura.
            sala = new Sala { Tipo = TipoSala.Grupo, EquipeId = equipeId };
            db.Salas.Add(sala);
            await db.SaveChangesAsync();
            await db.Entry(sala).Reference(s => s.Equipe).LoadAsync();
        }

        var ultimas = await UltimasMensagensAsync([sala.Id]);

        return Ok(SalaResumoDto.Canal(sala, ultimas.GetValueOrDefault(sala.Id)));
    }

    /// <summary>Historico paginado: sem antesDe, o fim da conversa; com ele, o pedaco anterior.</summary>
    [HttpGet("{id:guid}/mensagens")]
    public async Task<ActionResult<PaginaMensagensDto>> Mensagens(
        Guid id, int limite = 50, DateTimeOffset? antesDe = null)
    {
        var sala = await db.Salas.FindAsync(id);
        if (sala is null) return NotFound(new { erro = "Sala nao encontrada." });
        if (!(await ParticipantesAsync(sala)).Contains(MeuId)) return Forbid();

        limite = Math.Clamp(limite, 1, 200);
        var consulta = db.Mensagens.Where(m => m.SalaId == id);
        if (antesDe is DateTimeOffset cursor)
        {
            var limiteUtc = cursor.UtcDateTime;
            consulta = consulta.Where(m => m.EnviadaEm < limiteUtc);
        }

        // Busca um a mais so para saber se existe pagina anterior.
        var mensagens = await consulta
            .Include(m => m.Autor)
            .OrderByDescending(m => m.EnviadaEm)
            .Take(limite + 1)
            .ToListAsync();

        var temMais = mensagens.Count > limite;
        var pagina = mensagens.Take(limite).Reverse().Select(MensagemDto.De);
        return Ok(new PaginaMensagensDto(pagina, temMais));
    }

    /// <summary>Grava a mensagem e avisa os participantes pelo hub (o autor tambem recebe).</summary>
    [HttpPost("{id:guid}/mensagens")]
    public async Task<ActionResult<MensagemDto>> Enviar(Guid id, EnviarMensagemDto dto)
    {
        var sala = await db.Salas.FindAsync(id);
        if (sala is null) return NotFound(new { erro = "Sala nao encontrada." });
        var participantes = await ParticipantesAsync(sala);
        if (!participantes.Contains(MeuId)) return Forbid();

        var texto = dto.Texto.Trim();
        if (texto.Length == 0) return BadRequest(new { erro = "A mensagem nao pode ficar vazia." });

        var mensagem = new Mensagem { SalaId = id, AutorId = MeuId, Texto = texto };
        db.Mensagens.Add(mensagem);
        await db.SaveChangesAsync();

        await db.Entry(mensagem).Reference(m => m.Autor).LoadAsync();
        var mapeada = MensagemDto.De(mensagem);

        // ToString() no C#, nao no SQL: o SQLite devolveria o GUID em maiusculas
        // e o Clients.Users compara com o "sub" do token, que e minusculo.
        await hub.Clients
            .Users(participantes.Select(u => u.ToString()).ToList())
            .SendAsync("MensagemRecebida", mapeada);

        return Ok(mapeada);
    }

    // ---- apoio

    private Guid MeuId => permissoes.IdLogado ?? Guid.Empty;

    /// <summary>Quem participa: na privada, a lista gravada; no canal, quem esta na equipe agora.
    /// A mesma lista autoriza e escolhe quem recebe pelo hub.</summary>
    private async Task<List<Guid>> ParticipantesAsync(Sala sala)
    {
        if (sala.Tipo == TipoSala.Privada)
        {
            return await db.SalaUsuarios
                .Where(p => p.SalaId == sala.Id)
                .Select(p => p.UsuarioId)
                .ToListAsync();
        }

        // Canal: participante e quem esta na equipe agora.
        if (sala.EquipeId is not Guid equipeId) return [];

        return await db.Users
            .Where(u => u.EquipeId == equipeId)
            .Select(u => u.Id)
            .ToListAsync();
    }

    /// <summary>Ultima mensagem de cada sala, numa consulta so.</summary>
    private async Task<Dictionary<Guid, Mensagem>> UltimasMensagensAsync(List<Guid> salaIds)
    {
        var ultimas = await db.Salas
            .Where(s => salaIds.Contains(s.Id))
            .Select(s => s.Mensagens.OrderByDescending(m => m.EnviadaEm).FirstOrDefault())
            .ToListAsync();

        return ultimas.Where(m => m is not null).ToDictionary(m => m!.SalaId, m => m!);
    }
}
