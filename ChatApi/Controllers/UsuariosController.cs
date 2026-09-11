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
[Route("api/usuarios")]
public class UsuariosController(AppDbContext db, Permissoes permissoes) : ControllerBase
{
    /// <summary>Lista enxuta (sem e-mail) usada nos formularios de cargos e
    /// equipes e na tela de pessoas. Endpoints de perfil continuam sendo a
    /// fonte para dados sensiveis.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PessoaDto>>> Listar()
    {
        var usuarios = await db.Users
            .Include(u => u.Cargo)
            .Include(u => u.Equipe)
            .OrderBy(u => u.NomeCompleto)
            .ToListAsync();

        return Ok(usuarios.Select(MapeamentoOrganizacao.ParaPessoa));
    }

    /// <summary>Atribui (ou remove, com CargoId nulo) o cargo de uma pessoa.
    /// Quem pode fazer o que esta em <see cref="Permissoes.PodeAtribuirAsync"/>.</summary>
    [HttpPut("{id:guid}/cargo")]
    public async Task<ActionResult<PessoaDto>> AtribuirCargo(Guid id, AtribuirCargoDto dto)
    {
        var alvo = await db.Users
            .Include(u => u.Cargo)
            .Include(u => u.Equipe)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (alvo is null) return NotFound(new { erro = "Usuario nao encontrado." });

        Cargo? novoCargo = null;
        if (dto.CargoId is Guid cargoId)
        {
            novoCargo = await db.Cargos.FindAsync(cargoId);
            if (novoCargo is null) return NotFound(new { erro = "Cargo nao encontrado." });
        }

        if (!await permissoes.PodeAtribuirAsync(novoCargo, alvo))
            return Forbid();

        if (await DeixariaSemDiretorAsync(alvo, novoCargo))
            return BadRequest(new
            {
                erro = "Este e o unico diretor. Promova outra pessoa a diretor antes de mudar este cargo."
            });

        alvo.CargoId = novoCargo?.Id;
        alvo.Cargo = novoCargo;
        await db.SaveChangesAsync();

        return Ok(MapeamentoOrganizacao.ParaPessoa(alvo));
    }

    /// <summary>
    /// Sem nenhum diretor ninguem consegue mais gerenciar cargos, e a unica
    /// saida seria apagar o banco. Um diretor pode se rebaixar — desde que
    /// exista outro.
    /// </summary>
    private async Task<bool> DeixariaSemDiretorAsync(Usuario alvo, Cargo? novoCargo)
    {
        if (alvo.Cargo?.Nivel != NivelHierarquico.Diretor) return false;
        if (novoCargo?.Nivel == NivelHierarquico.Diretor) return false;

        var diretores = await db.Users
            .CountAsync(u => u.Cargo!.Nivel == NivelHierarquico.Diretor);

        return diretores <= 1;
    }
}
