using System.Security.Claims;
using ChatApi.Data;
using ChatApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Autorizacao;

/// <summary>
/// Leitura do usuario que assinou a requisicao. O id vem do token (claim
/// "sub"), mas o nivel hierarquico vem do banco: o JWT vive 8 horas e uma
/// promocao precisa valer na proxima requisicao, nao no proximo login.
/// </summary>
public static class UsuarioLogado
{
    public static Guid? Id(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out var id) ? id : null;

    public static async Task<NivelHierarquico?> NivelAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        if (Id(principal) is not Guid usuarioId) return null;

        return await db.Users
            .Where(u => u.Id == usuarioId)
            .Select(u => u.Cargo != null ? u.Cargo.Nivel : (NivelHierarquico?)null)
            .FirstOrDefaultAsync();
    }
}
