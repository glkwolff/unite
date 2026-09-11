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
public class EquipesController(AppDbContext db, Permissoes permissoes) : ControllerBase
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

        // Diretor e gerente ficam no topo, fora das equipes. Antes so o
        // gerente era tratado assim e o diretor caia no balde "sem equipe".
        var lideranca = usuarios
            .Where(EstaNaLideranca)
            .Select(MapeamentoOrganizacao.ParaResumo);

        var semEquipe = usuarios
            .Where(u => u.EquipeId is null && !EstaNaLideranca(u))
            .Select(MapeamentoOrganizacao.ParaResumo);

        return Ok(new ArvoreOrganizacionalDto(lideranca, equipes.Select(Mapear), semEquipe));
    }

    [HttpPost]
    public async Task<ActionResult<EquipeDto>> Criar(EquipeEntradaDto dto)
    {
        if (!await permissoes.PodeGerenciarEquipesAsync())
            return Forbid();

        if (dto.SupervisorId is Guid supervisorId && !await db.Users.AnyAsync(u => u.Id == supervisorId))
            return BadRequest(new { erro = "Supervisor informado nao existe." });

        var equipe = new Equipe { Nome = dto.Nome.Trim(), SupervisorId = dto.SupervisorId };
        db.Equipes.Add(equipe);
        await db.SaveChangesAsync();

        return Ok(await ObterMapeadaAsync(equipe.Id));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EquipeDto>> Atualizar(Guid id, EquipeEntradaDto dto)
    {
        if (!await permissoes.PodeGerenciarEquipesAsync())
            return Forbid();

        var equipe = await db.Equipes.FindAsync(id);
        if (equipe is null) return NotFound(new { erro = "Equipe nao encontrada." });

        if (dto.SupervisorId is Guid supervisorId && !await db.Users.AnyAsync(u => u.Id == supervisorId))
            return BadRequest(new { erro = "Supervisor informado nao existe." });

        equipe.Nome = dto.Nome.Trim();
        equipe.SupervisorId = dto.SupervisorId;
        await db.SaveChangesAsync();

        return Ok(await ObterMapeadaAsync(equipe.Id));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(Guid id)
    {
        if (!await permissoes.PodeGerenciarEquipesAsync())
            return Forbid();

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
        // Gerente para cima mexe em qualquer equipe; o supervisor, so na dele.
        if (!await permissoes.PodeGerenciarMembrosAsync(id))
            return Forbid();

        var equipe = await db.Equipes.AnyAsync(e => e.Id == id);
        if (!equipe) return NotFound(new { erro = "Equipe nao encontrada." });

        var usuario = await db.Users.FindAsync(dto.UsuarioId);
        if (usuario is null) return NotFound(new { erro = "Usuario nao encontrado." });

        usuario.EquipeId = id;
        await db.SaveChangesAsync();

        return Ok(await ObterMapeadaAsync(id));
    }

    [HttpDelete("{id:guid}/membros/{usuarioId:guid}")]
    public async Task<ActionResult<EquipeDto>> RemoverMembro(Guid id, Guid usuarioId)
    {
        if (!await permissoes.PodeGerenciarMembrosAsync(id))
            return Forbid();

        var usuario = await db.Users.FirstOrDefaultAsync(u => u.Id == usuarioId && u.EquipeId == id);
        if (usuario is null) return NotFound(new { erro = "Membro nao encontrado nesta equipe." });

        usuario.EquipeId = null;
        await db.SaveChangesAsync();

        return Ok(await ObterMapeadaAsync(id));
    }

    /// <summary>Diretoria e gerencia nao pertencem a uma equipe: lideram todas.</summary>
    private static bool EstaNaLideranca(Usuario u) => u.Cargo?.Nivel <= NivelHierarquico.Gerente;

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
