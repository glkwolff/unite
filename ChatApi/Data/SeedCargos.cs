using ChatApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Data;

public static class SeedCargos
{
    private static readonly (string Nome, NivelHierarquico Nivel)[] Padrao =
    [
        ("Diretor", NivelHierarquico.Diretor),
        ("Gerente", NivelHierarquico.Gerente),
        ("Supervisor", NivelHierarquico.Supervisor),
        ("Funcionario", NivelHierarquico.Funcionario)
    ];

    public static async Task GarantirAsync(AppDbContext db)
    {
        if (await db.Cargos.AnyAsync()) return;

        db.Cargos.AddRange(Padrao.Select(c => new Cargo { Nome = c.Nome, Nivel = c.Nivel }));
        await db.SaveChangesAsync();
    }
}
