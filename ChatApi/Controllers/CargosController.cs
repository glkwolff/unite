using ChatApi.Data;
using ChatApi.Dtos;
using ChatApi.Models;
using ChatApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Controllers;

// Qualquer autenticado le a lista de cargos (o organograma depende dela);
// so o diretor escreve.
[Authorize]
[ApiController]
[Route("api/cargos")]
public class CargosController(AppDbContext db, Permissoes permissoes) : ControllerBase
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

    [HttpPost]
    public async Task<ActionResult<CargoDto>> Criar(CargoEntradaDto dto)
    {
        if (!await permissoes.PodeGerenciarCargosAsync())
            return Forbid();

        var cargo = new Cargo { Nome = dto.Nome.Trim(), Nivel = dto.Nivel };
        db.Cargos.Add(cargo);
        await db.SaveChangesAsync();

        return Ok(new CargoDto(cargo.Id, cargo.Nome, cargo.Nivel, 0));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CargoDto>> Atualizar(Guid id, CargoEntradaDto dto)
    {
        if (!await permissoes.PodeGerenciarCargosAsync())
            return Forbid();

        var cargo = await db.Cargos.FindAsync(id);
        if (cargo is null) return NotFound(new { erro = "Cargo nao encontrado." });

        cargo.Nome = dto.Nome.Trim();
        cargo.Nivel = dto.Nivel;
        await db.SaveChangesAsync();

        var total = await db.Users.CountAsync(u => u.CargoId == id);
        return Ok(new CargoDto(cargo.Id, cargo.Nome, cargo.Nivel, total));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(Guid id)
    {
        if (!await permissoes.PodeGerenciarCargosAsync())
            return Forbid();

        var cargo = await db.Cargos.FindAsync(id);
        if (cargo is null) return NotFound(new { erro = "Cargo nao encontrado." });

        // O SetNull do DbContext nao apaga ninguem, mas deixaria todo mundo
        // desse cargo sem nivel. Apagar o cargo de diretor com gente dentro
        // tiraria a diretoria inteira de uma vez: ninguem mais gerenciaria
        // cargos e nao haveria volta pela aplicacao, porque o SeedCargos so
        // age com a tabela vazia. Esvaziar o cargo antes e explicito e
        // reversivel.
        if (await db.Users.AnyAsync(u => u.CargoId == id))
            return BadRequest(new
            {
                erro = "Este cargo ainda tem pessoas: troque o cargo delas antes de remove-lo."
            });

        db.Cargos.Remove(cargo);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
