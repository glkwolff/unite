using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace ChatApi.Tests.Apoio;

public class UniteFactory : WebApplicationFactory<Program>
{
    private readonly string _pasta = Path.Combine(
        Path.GetTempPath(), "unite-testes", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_pasta);
        var webroot = Path.Combine(_pasta, "wwwroot");
        Directory.CreateDirectory(webroot);

        builder.UseWebRoot(webroot);
        builder.UseSetting("ConnectionStrings:Padrao", $"Data Source={Path.Combine(_pasta, "testes.db")}");
        builder.UseSetting("Jwt:Chave", "chave-so-para-os-testes-de-integracao-com-mais-de-32-bytes");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_pasta, recursive: true); } catch { }
    }
}
