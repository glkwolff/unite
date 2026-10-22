using ChatApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Data;

public static class SeedCargos
{
    /// <summary>Um cargo por nivel; depois o diretor pode criar outros.</summary>
    private static readonly (string Nome, NivelHierarquico Nivel)[] Padrao =
    [
        ("Diretor", NivelHierarquico.Diretor),
        ("Gerente", NivelHierarquico.Gerente),
        ("Supervisor", NivelHierarquico.Supervisor),
        ("Funcionario", NivelHierarquico.Funcionario)
    ];

    /// <summary>Roda no startup; so age se a tabela de cargos estiver vazia.</summary>
    public static async Task GarantirAsync(AppDbContext db)
    {
        if (await db.Cargos.AnyAsync()) return;

        db.Cargos.AddRange(Padrao.Select(c => new Cargo { Nome = c.Nome, Nivel = c.Nivel }));
        await db.SaveChangesAsync();
    }
}
