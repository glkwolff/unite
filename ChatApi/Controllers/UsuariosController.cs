using ChatApi.Data;
using ChatApi.Dtos;
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
}
