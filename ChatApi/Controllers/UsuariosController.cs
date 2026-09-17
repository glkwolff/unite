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
[Route("api/usuarios")]
public class UsuariosController(AppDbContext db) : ControllerBase
{
    /// <summary>Lista enxuta (sem e-mail) usada nos formularios de cargos e
    /// equipes. Endpoints de perfil continuam sendo a fonte para dados sensiveis.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MembroResumoDto>>> Listar()
    {
        var usuarios = await db.Users
            .Include(u => u.Cargo)
            .OrderBy(u => u.NomeCompleto)
            .ToListAsync();

        return Ok(usuarios.Select(MapeamentoOrganizacao.ParaResumo));
    }

    /// <summary>
    /// Promove ou rebaixa alguem. O [ExigeNivel] garante o piso (gerencia);
    /// as travas de quem promove quem ficam em <see cref="Permissoes"/>.
    /// </summary>
    [ExigeNivel(NivelHierarquico.Gerente)]
    [HttpPut("{id:guid}/cargo")]
    public async Task<ActionResult<MembroResumoDto>> DefinirCargo(Guid id, DefinirCargoDto dto)
    {
        var usuario = await db.Users.Include(u => u.Cargo).FirstOrDefaultAsync(u => u.Id == id);
        if (usuario is null) return NotFound(new { erro = "Usuario nao encontrado." });

        Cargo? cargo = null;
        if (dto.CargoId is Guid cargoId)
        {
            cargo = await db.Cargos.FindAsync(cargoId);
            if (cargo is null) return BadRequest(new { erro = "Cargo informado nao existe." });
        }

        var meuNivel = await UsuarioLogado.NivelAsync(User, db);
        var meuId = UsuarioLogado.Id(User) ?? Guid.Empty;

        // Tirar o cargo de alguem tem o mesmo peso de dar o mais alto: vale a
        // mesma regra, com o nivel do proprio autor como referencia.
        var nivelPretendido = cargo?.Nivel ?? meuNivel ?? NivelHierarquico.Funcionario;

        if (!Permissoes.PodeAtribuirCargo(meuNivel, meuId, id, nivelPretendido))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                erro = "Voce nao pode alterar o proprio cargo nem atribuir um cargo acima do seu."
            });
        }

        usuario.CargoId = cargo?.Id;
        usuario.Cargo = cargo;
        await db.SaveChangesAsync();

        return Ok(MapeamentoOrganizacao.ParaResumo(usuario));
    }

    /// <summary>Lota a pessoa em uma equipe (ou tira dela, com equipeId nulo).</summary>
    [ExigeNivel(NivelHierarquico.Gerente)]
    [HttpPut("{id:guid}/equipe")]
    public async Task<ActionResult<MembroResumoDto>> DefinirEquipe(Guid id, DefinirEquipeDto dto)
    {
        var usuario = await db.Users.Include(u => u.Cargo).FirstOrDefaultAsync(u => u.Id == id);
        if (usuario is null) return NotFound(new { erro = "Usuario nao encontrado." });

        if (dto.EquipeId is Guid equipeId && !await db.Equipes.AnyAsync(e => e.Id == equipeId))
            return BadRequest(new { erro = "Equipe informada nao existe." });

        usuario.EquipeId = dto.EquipeId;
        await db.SaveChangesAsync();

        return Ok(MapeamentoOrganizacao.ParaResumo(usuario));
    }
}
