using ChatApi.Autorizacao;
using ChatApi.Data;
using ChatApi.Dtos;
using ChatApi.Models;
using ChatApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Controllers;

[Authorize]
[ApiController]
[Route("api/equipes")]
public class EquipesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EquipeDto>>> Listar()
    {
        var equipes = await db.Equipes
            .Include(e => e.Supervisor).ThenInclude(s => s!.Cargo)
            .Include(e => e.Membros).ThenInclude(m => m.Cargo)
            .OrderBy(e => e.Nome)
            .ToListAsync();

        return Ok(equipes.Select(Mapear));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EquipeDto>> Obter(Guid id)
    {
        var equipe = await db.Equipes
            .Include(e => e.Supervisor).ThenInclude(s => s!.Cargo)
            .Include(e => e.Membros).ThenInclude(m => m.Cargo)
            .FirstOrDefaultAsync(e => e.Id == id);

        return equipe is null ? NotFound(new { erro = "Equipe nao encontrada." }) : Ok(Mapear(equipe));
    }

    /// <summary>Monta o organograma inteiro em uma chamada so — evita o front
    /// ter que cruzar /equipes com /cargos para desenhar a arvore.</summary>
    [HttpGet("arvore")]
    public async Task<ActionResult<ArvoreOrganizacionalDto>> Arvore()
    {
        var usuarios = await db.Users.Include(u => u.Cargo).ToListAsync();
        var equipes = await db.Equipes
            .Include(e => e.Supervisor).ThenInclude(s => s!.Cargo)
            .Include(e => e.Membros).ThenInclude(m => m.Cargo)
            .OrderBy(e => e.Nome)
            .ToListAsync();

        // Topo da arvore: quem administra a estrutura (diretoria e gerencia).
        // Nao aparecem de novo em "sem equipe", mesmo sem equipe definida.
        var gerentes = usuarios
            .Where(u => Permissoes.PodeAdministrar(u.Cargo?.Nivel))
            .Select(MapeamentoOrganizacao.ParaResumo);

        var semEquipe = usuarios
            .Where(u => u.EquipeId is null && !Permissoes.PodeAdministrar(u.Cargo?.Nivel))
            .Select(MapeamentoOrganizacao.ParaResumo);

        return Ok(new ArvoreOrganizacionalDto(gerentes, equipes.Select(Mapear), semEquipe));
    }

    [ExigeNivel(NivelHierarquico.Gerente)]
    [HttpPost]
    public async Task<ActionResult<EquipeDto>> Criar(EquipeEntradaDto dto)
    {
        if (dto.SupervisorId is Guid supervisorId && !await db.Users.AnyAsync(u => u.Id == supervisorId))
            return BadRequest(new { erro = "Supervisor informado nao existe." });

        var equipe = new Equipe { Nome = dto.Nome.Trim(), SupervisorId = dto.SupervisorId };
        db.Equipes.Add(equipe);
        await db.SaveChangesAsync();

        return Ok(await ObterMapeadaAsync(equipe.Id));
    }

    [ExigeNivel(NivelHierarquico.Gerente)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EquipeDto>> Atualizar(Guid id, EquipeEntradaDto dto)
    {
        var equipe = await db.Equipes.FindAsync(id);
        if (equipe is null) return NotFound(new { erro = "Equipe nao encontrada." });

        if (dto.SupervisorId is Guid supervisorId && !await db.Users.AnyAsync(u => u.Id == supervisorId))
            return BadRequest(new { erro = "Supervisor informado nao existe." });

        equipe.Nome = dto.Nome.Trim();
        equipe.SupervisorId = dto.SupervisorId;
        await db.SaveChangesAsync();

        return Ok(await ObterMapeadaAsync(equipe.Id));
    }

    [ExigeNivel(NivelHierarquico.Gerente)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(Guid id)
    {
        var equipe = await db.Equipes.FindAsync(id);
        if (equipe is null) return NotFound(new { erro = "Equipe nao encontrada." });

        // Membros ficam sem equipe (SetNull) em vez de serem apagados junto.
        db.Equipes.Remove(equipe);
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/membros")]
    public async Task<ActionResult<EquipeDto>> AdicionarMembro(Guid id, AdicionarMembroDto dto)
    {
        var equipe = await db.Equipes.FirstOrDefaultAsync(e => e.Id == id);
        if (equipe is null) return NotFound(new { erro = "Equipe nao encontrada." });

        if (await NegarSeNaoGerenciaAsync(equipe) is ObjectResult negado) return negado;

        var usuario = await db.Users.FindAsync(dto.UsuarioId);
        if (usuario is null) return NotFound(new { erro = "Usuario nao encontrado." });

        usuario.EquipeId = id;
        await db.SaveChangesAsync();

        return Ok(await ObterMapeadaAsync(id));
    }

    [HttpDelete("{id:guid}/membros/{usuarioId:guid}")]
    public async Task<ActionResult<EquipeDto>> RemoverMembro(Guid id, Guid usuarioId)
    {
        var equipe = await db.Equipes.FirstOrDefaultAsync(e => e.Id == id);
        if (equipe is null) return NotFound(new { erro = "Equipe nao encontrada." });

        if (await NegarSeNaoGerenciaAsync(equipe) is ObjectResult negado) return negado;

        var usuario = await db.Users.FirstOrDefaultAsync(u => u.Id == usuarioId && u.EquipeId == id);
        if (usuario is null) return NotFound(new { erro = "Membro nao encontrado nesta equipe." });

        usuario.EquipeId = null;
        await db.SaveChangesAsync();

        return Ok(await ObterMapeadaAsync(id));
    }

    /// <summary>Lotar e desligar membros nao usa [ExigeNivel] porque a regra
    /// depende da equipe: o supervisor so manda na equipe que ele responde.
    /// Devolve null quando a acao esta liberada.</summary>
    private async Task<ObjectResult?> NegarSeNaoGerenciaAsync(Equipe equipe)
    {
        var nivel = await UsuarioLogado.NivelAsync(User, db);
        var usuarioId = UsuarioLogado.Id(User) ?? Guid.Empty;

        if (Permissoes.PodeGerenciarMembros(nivel, usuarioId, equipe.SupervisorId))
            return null;

        return new ObjectResult(new
        {
            erro = "Apenas a gerencia ou o supervisor desta equipe pode alterar os membros."
        })
        { StatusCode = StatusCodes.Status403Forbidden };
    }

    private async Task<EquipeDto> ObterMapeadaAsync(Guid id)
    {
        var equipe = await db.Equipes
            .Include(e => e.Supervisor).ThenInclude(s => s!.Cargo)
            .Include(e => e.Membros).ThenInclude(m => m.Cargo)
            .FirstAsync(e => e.Id == id);

        return Mapear(equipe);
    }

    private static EquipeDto Mapear(Equipe e) => new(
        e.Id,
        e.Nome,
        e.Supervisor is null ? null : MapeamentoOrganizacao.ParaResumo(e.Supervisor),
        e.Membros.Select(MapeamentoOrganizacao.ParaResumo));
}
