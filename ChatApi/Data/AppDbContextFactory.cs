using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ChatApi.Data;

/// <summary>So para o "dotnet ef": gera migrations sem precisar da chave JWT.</summary>
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
