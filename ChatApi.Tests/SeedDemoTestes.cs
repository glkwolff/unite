using System.Net.Http.Json;
using System.Text.Json;
using ChatApi.Data;
using ChatApi.Dtos;
using ChatApi.Models;
using ChatApi.Tests.Apoio;
using Microsoft.Extensions.DependencyInjection;

namespace ChatApi.Tests;

/// <summary>A empresa de demonstracao sobe numa base vazia e e lida pela propria API.</summary>
public class SeedDemoTestes : IDisposable
{
    private readonly UniteFactory _fabrica = new();

    public void Dispose() => _fabrica.Dispose();

    private async Task<bool> SemearAsync()
    {
        using var escopo = _fabrica.Services.CreateScope();
        return await SeedDemo.ExecutarAsync(escopo.ServiceProvider);
    }

    private async Task<HttpClient> EntrarAsync(string login)
    {
        var cliente = _fabrica.CreateClient();
        var resposta = await cliente.PostAsync("/api/auth/login",
            new LoginDto { Email = $"{login}@unite.demo", Senha = SeedDemo.Senha });
        resposta.EnsureSuccessStatusCode();

        var auth = (await resposta.LerAsync<AuthRespostaDto>())!;
        cliente.DefaultRequestAuthorization(auth.Token);
        return cliente;
    }

    [Fact]
    public async Task Semeia_organograma_feed_e_conversas()
    {
        Assert.True(await SemearAsync());

        var camila = await EntrarAsync("camila.rocha");

        var eu = await camila.GetAsync<UsuarioDto>("/api/auth/eu");
        Assert.Equal("Executivo de Vendas", eu!.Cargo);
        Assert.Equal(NivelHierarquico.Funcionario, eu.Nivel);
        Assert.Equal("Vendas", eu.Equipe);

        var feed = await camila.GetAsync<PaginaFeedDto>("/api/postagens?limite=50");
        var avisos = feed!.Itens.ToList();
        Assert.Equal(8, avisos.Count);
        Assert.Contains(avisos, p => p.Institucional);
        // Os avisos recentes ficam sem "Ciente" para a conta de apresentacao clicar.
        Assert.Null(avisos[0].CienteEm);
        Assert.True(avisos[0].PodeMarcarCiente);
        using (var escopo = _fabrica.Services.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(db.Ciencias.Count() > 20);
            Assert.DoesNotContain(db.Ciencias, c => c.ConfirmadaEm > DateTime.UtcNow);
        }

        var salas = await camila.GetAsync<List<SalaResumoDto>>("/api/salas");
        Assert.Contains(salas!, s => s.Tipo == TipoSala.Grupo);
        Assert.Contains(salas!, s => s.Tipo == TipoSala.Privada);

        var arvore = await camila.GetFromJsonAsync<JsonElement>("/api/equipes/arvore", Ambiente.Json);
        Assert.Contains("Desenvolvimento", arvore.GetRawText());
    }

    [Fact]
    public async Task Diretoria_cai_na_configuracao_do_2fa_e_segunda_execucao_nao_duplica()
    {
        Assert.True(await SemearAsync());
        Assert.False(await SemearAsync());

        var resposta = await _fabrica.CreateClient().PostAsync("/api/auth/login",
            new LoginDto { Email = "helena.duarte@unite.demo", Senha = SeedDemo.Senha });
        var etapa = await resposta.LerAsync<SegundaEtapaDto>();
        Assert.Equal("configurar", etapa!.Etapa);
    }
}
