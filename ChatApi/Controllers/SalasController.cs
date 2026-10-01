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
        var meuId = permissoes.IdLogado ?? Guid.Empty;
        var minhaEquipe = (await permissoes.UsuarioAtualAsync())?.EquipeId;

        var privadas = await db.Salas
            .Where(s => s.Tipo == TipoSala.Privada && s.Participantes.Any(p => p.UsuarioId == meuId))
            .Include(s => s.Participantes).ThenInclude(p => p.Usuario).ThenInclude(u => u!.Cargo)
            .ToListAsync();

        // Duas consultas em vez de um OR num Where so: "minha sala" e uma regra
        // diferente em cada tipo, e juntar as duas num OR com EquipeId nulo
        // casaria a minha ausencia de equipe com o canal orfao de uma equipe
        // apagada. Sem equipe simplesmente nao ha canal para listar.
        var canais = minhaEquipe is Guid equipeId
            ? await db.Salas
                .Where(s => s.Tipo == TipoSala.Grupo && s.EquipeId == equipeId)
                .Include(s => s.Equipe)
                .ToListAsync()
            : [];

        var ultimas = await UltimasMensagensAsync(
            privadas.Concat(canais).Select(s => s.Id).ToList());

        var resumos = privadas
            .Select(s => MapearPrivada(s, meuId, ultimas.GetValueOrDefault(s.Id)))
            .Where(r => r is not null)
            .Concat(canais.Select(s => MapearCanal(s, ultimas.GetValueOrDefault(s.Id))));

        return Ok(resumos.OrderByDescending(r => r!.UltimaEm ?? DateTime.MinValue));
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

        return Ok(MapearPrivada(sala, meuId, ultimas.GetValueOrDefault(sala.Id)));
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

        return Ok(MapearCanal(sala, ultimas.GetValueOrDefault(sala.Id)));
    }

    /// <summary>
    /// Historico da sala, em ordem de leitura. Sem <paramref name="antesDe"/>
    /// devolve o fim da conversa; com ele, o pedaco imediatamente anterior — e
    /// assim que o front carrega conversa antiga ao rolar para cima.
    ///
    /// A resposta continua sendo um array puro: "ainda tem mais" se descobre
    /// pela contagem, porque vir o limite cheio significa que pode haver mais.
    /// Um envelope { itens, temMais } mudaria o contrato ja publicado para dar
    /// um sinal que o tamanho da resposta ja da.
    /// </summary>
    [HttpGet("{id:guid}/mensagens")]
    public async Task<ActionResult<IEnumerable<MensagemDto>>> Mensagens(
        Guid id, int limite = 50, DateTime? antesDe = null)
    {
        var (_, negado) = await ParticipacaoAsync(id);
        if (negado is not null) return negado;

        var consulta = db.Mensagens.Where(m => m.SalaId == id);

        // Filtro em passo separado, e nao um "antesDe == null || ..." dentro do
        // Where: assim o SQL sai sem OR e usa direto o indice (SalaId, EnviadaEm).
        //
        // A comparacao e estrita. Duas mensagens com EnviadaEm identico fariam
        // uma delas ser pulada, mas EnviadaEm vem de DateTime.UtcNow, com
        // resolucao de 100 nanossegundos: o empate exigiria dois inserts no
        // mesmo tick da mesma sala. Desempatar por Id ordenaria Guid como texto
        // no SQLite — ordem estavel, porem sem significado algum.
        if (antesDe is DateTime cursor)
            consulta = consulta.Where(m => m.EnviadaEm < cursor);

        var mensagens = await consulta
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
        var (participantes, negado) = await ParticipacaoAsync(id);
        if (negado is not null) return negado;

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

        // Os destinatarios sao a mesma lista que autorizou a entrada, para o
        // envio nunca mirar gente diferente da que a autorizacao considerou. No
        // canal ela e recalculada a cada mensagem, entao quem entrou na equipe
        // agora recebe a proxima sem religar a conexao.
        //
        // O ToString() fica no C# de proposito: traduzido para SQL, o SQLite
        // devolveria o GUID em maiusculas, e o Clients.Users compara com o
        // "sub" do token (minusculo) caractere a caractere — ninguem receberia.
        await hub.Clients
            .Users(participantes!.Select(d => d.ToString()).ToList())
            .SendAsync("MensagemRecebida", mapeada);

        return Ok(mapeada);
    }

    // ------------------------------------------------------------- apoio

    /// <summary>
    /// Responde de uma vez a pergunta que toda acao de sala faz: eu participo
    /// desta sala e, se participo, quem mais participa? Devolve a recusa pronta
    /// (404 para sala inexistente, 403 para quem esta de fora) ou a lista de
    /// participantes. Os dois vem juntos porque "quem participa" E a regra de
    /// acesso: separar abriria espaco para o broadcast rodar sem a checagem.
    /// </summary>
    private async Task<(List<Guid>? Participantes, ObjectResult? Negado)> ParticipacaoAsync(Guid salaId)
    {
        var sala = await db.Salas.FirstOrDefaultAsync(s => s.Id == salaId);
        if (sala is null) return (null, NotFound(new { erro = "Sala nao encontrada." }));

        var participantes = await ParticipantesAsync(sala);
        var meuId = permissoes.IdLogado ?? Guid.Empty;

        if (!participantes.Contains(meuId))
        {
            return (null, StatusCode(StatusCodes.Status403Forbidden, new
            {
                erro = "Voce nao participa desta conversa."
            }));
        }

        return (participantes, null);
    }

    /// <summary>
    /// Os dois caminhos de participacao, lado a lado: a privada guarda a propria
    /// lista em SalaUsuarios, e o canal de equipe nao guarda nada — participante
    /// dele e quem esta na equipe agora. Devolve os ids em vez de so responder
    /// "sim ou nao" porque a mesma lista alimenta o broadcast, e uma equipe tem
    /// dezenas de pessoas, nao milhares.
    /// </summary>
    private async Task<List<Guid>> ParticipantesAsync(Sala sala)
    {
        if (sala.Tipo == TipoSala.Privada)
        {
            return await db.SalaUsuarios
                .Where(p => p.SalaId == sala.Id)
                .Select(p => p.UsuarioId)
                .ToListAsync();
        }

        // O Guid e aberto ANTES da consulta de proposito. O EF Core compensa a
        // semantica de nulo do SQL, entao comparar EquipeId com um parametro
        // nulo viraria "os dois IS NULL" e daria o canal orfao (equipe apagada)
        // para toda a lideranca, que nao tem equipe. Canal sem equipe nao tem
        // participante nenhum, e por isso fica fechado para todos.
        if (sala.EquipeId is not Guid equipeId) return [];

        return await db.Users
            .Where(u => u.EquipeId == equipeId)
            .Select(u => u.Id)
            .ToListAsync();
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

    private static SalaResumoDto? MapearPrivada(Sala sala, Guid meuId, Mensagem? ultima)
    {
        var outro = sala.Participantes.FirstOrDefault(p => p.UsuarioId != meuId)?.Usuario;
        // Sala privada sem o outro lado (conta removida) nao tem o que mostrar.
        if (outro is null) return null;

        return new SalaResumoDto(
            sala.Id,
            TipoSala.Privada,
            outro.NomeCompleto,
            MapeamentoOrganizacao.ParaResumo(outro),
            ultima?.Texto,
            ultima?.EnviadaEm);
    }

    /// <summary>Exige Include(s => s.Equipe): o titulo e o nome atual da equipe.</summary>
    private static SalaResumoDto MapearCanal(Sala sala, Mensagem? ultima) => new(
        sala.Id,
        TipoSala.Grupo,
        sala.Equipe?.Nome ?? "Canal sem equipe",
        null,
        ultima?.Texto,
        ultima?.EnviadaEm);

    private static MensagemDto Mapear(Mensagem m) => new(
        m.Id,
        m.SalaId,
        m.AutorId,
        m.Autor?.NomeCompleto ?? string.Empty,
        m.Texto,
        m.EnviadaEm);
}
