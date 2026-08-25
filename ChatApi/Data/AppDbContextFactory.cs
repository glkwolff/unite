using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ChatApi.Data;

/// <summary>
/// Usado apenas pelo "dotnet ef" em tempo de design. Sem esta fabrica o EF
/// precisaria subir o host da aplicacao, que exige a chave JWT configurada —
/// e migrations nao deveriam depender de segredo nenhum.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=chat.db")
            .Options;

        return new AppDbContext(options);
    }
}
