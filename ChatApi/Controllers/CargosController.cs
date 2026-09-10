using ChatApi.Data;
using ChatApi.Dtos;
using ChatApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Controllers;

// Restricao por papel (RF11) fica para a Aula 3 — por ora, qualquer usuario
// autenticado monta a estrutura organizacional inicial.
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

    [HttpPost]
    public async Task<ActionResult<CargoDto>> Criar(CargoEntradaDto dto)
    {
        var cargo = new Cargo { Nome = dto.Nome.Trim(), Nivel = dto.Nivel };
        db.Cargos.Add(cargo);
        await db.SaveChangesAsync();

        return Ok(new CargoDto(cargo.Id, cargo.Nome, cargo.Nivel, 0));
    }

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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remover(Guid id)
    {
        var cargo = await db.Cargos.FindAsync(id);
        if (cargo is null) return NotFound(new { erro = "Cargo nao encontrado." });

        // Quem tinha esse cargo fica sem cargo (SetNull ja configurado no
        // DbContext) — ninguem e apagado por causa disso.
        db.Cargos.Remove(cargo);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
