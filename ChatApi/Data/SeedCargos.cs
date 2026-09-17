using ChatApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Data;

public static class SeedCargos
{
    /// <summary>Nome padrao de cada nivel. O nome do cargo e texto livre —
    /// estes sao so os quatro que toda empresa tem no primeiro dia.</summary>
    private static readonly (string Nome, NivelHierarquico Nivel)[] Padrao =
    [
        ("Diretor", NivelHierarquico.Diretor),
        ("Gerente", NivelHierarquico.Gerente),
        ("Supervisor", NivelHierarquico.Supervisor),
        ("Funcionario", NivelHierarquico.Funcionario)
    ];

    /// <summary>
    /// Numa base nova nao existe nenhum cargo, entao nao haveria o que atribuir
    /// ao primeiro usuario — nem como sair do lugar. Roda no startup e nao faz
    /// nada se ja houver qualquer cargo cadastrado.
    /// </summary>
    public static async Task GarantirAsync(AppDbContext db)
    {
        if (await db.Cargos.AnyAsync()) return;

        db.Cargos.AddRange(Padrao.Select(c => new Cargo { Nome = c.Nome, Nivel = c.Nivel }));
        await db.SaveChangesAsync();
    }
}
