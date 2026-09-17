using ChatApi.Autorizacao;
using ChatApi.Data;
using ChatApi.Dtos;
using ChatApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Controllers;

// Ler a lista de cargos e liberado para qualquer autenticado (o front usa
// nos selects); criar, editar e apagar e da gerencia para cima (RF11).
[Authorize]
[ApiController]
[Route("api/cargos")]
public class CargosController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CargoDto>>> Listar()
    {
        var cargos = await db.Cargos
            .OrderBy(c => c.Nivel).ThenBy(c => c.Nome)
            .Select(c => new CargoDto(c.Id, c.Nome, c.Nivel, c.Usuarios.Count))
            .ToListAsync();

        return Ok(cargos);
    }

    [ExigeNivel(NivelHierarquico.Gerente)]
    [HttpPost]
    public async Task<ActionResult<CargoDto>> Criar(CargoEntradaDto dto)
    {
        var cargo = new Cargo { Nome = dto.Nome.Trim(), Nivel = dto.Nivel };
        db.Cargos.Add(cargo);
        await db.SaveChangesAsync();

        return Ok(new CargoDto(cargo.Id, cargo.Nome, cargo.Nivel, 0));
    }

    [ExigeNivel(NivelHierarquico.Gerente)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CargoDto>> Atualizar(Guid id, CargoEntradaDto dto)
    {
        var cargo = await db.Cargos.FindAsync(id);
        if (cargo is null) return NotFound(new { erro = "Cargo nao encontrado." });

        cargo.Nome = dto.Nome.Trim();
        cargo.Nivel = dto.Nivel;
        await db.SaveChangesAsync();

        var total = await db.Users.CountAsync(u => u.CargoId == id);
        return Ok(new CargoDto(cargo.Id, cargo.Nome, cargo.Nivel, total));
    }

    [ExigeNivel(NivelHierarquico.Gerente)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(Guid id)
    {
        var cargo = await db.Cargos.FindAsync(id);
        if (cargo is null) return NotFound(new { erro = "Cargo nao encontrado." });

        // Apagar um cargo em uso deixaria pessoas sem nivel — inclusive,
        // no pior caso, o ultimo diretor. Exige esvaziar o cargo antes.
        if (await db.Users.AnyAsync(u => u.CargoId == id))
            return BadRequest(new { erro = "Este cargo ainda tem pessoas: troque o cargo delas antes de remove-lo." });

        db.Cargos.Remove(cargo);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
