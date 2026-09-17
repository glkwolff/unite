using ChatApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Data;

/// <summary>
/// Banco novo comeca sem cargo nenhum — e, como as rotas administrativas
/// agora exigem um cargo, ninguem conseguiria criar o primeiro. Esta semente
/// resolve o impasse: um cargo por nivel, criado so quando a tabela esta vazia.
/// </summary>
public static class CargosPadrao
{
    public static async Task GarantirAsync(AppDbContext db)
    {
        if (await db.Cargos.AnyAsync()) return;

        db.Cargos.AddRange(
            new Cargo { Nome = "Diretor", Nivel = NivelHierarquico.Diretor },
            new Cargo { Nome = "Gerente", Nivel = NivelHierarquico.Gerente },
            new Cargo { Nome = "Supervisor", Nivel = NivelHierarquico.Supervisor },
            new Cargo { Nome = "Funcionario", Nivel = NivelHierarquico.Funcionario });

        await db.SaveChangesAsync();
    }
}
