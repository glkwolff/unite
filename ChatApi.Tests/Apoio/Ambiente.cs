using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChatApi.Dtos;
using ChatApi.Models;

namespace ChatApi.Tests.Apoio;

/// <summary>
/// Um banco novo com a diretoria ja logada (primeira conta = Diretor) e os 4 cargos padrao.
/// Compartilhado pelos testes de uma classe (IClassFixture): cada teste cria as contas que precisa
/// com e-mails unicos, entao um nao enxerga o outro.
/// </summary>
public class Ambiente : IAsyncLifetime
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public UniteFactory Fabrica { get; } = new();

    public Conta Diretor { get; private set; } = null!;

    private readonly Dictionary<NivelHierarquico, Guid> _cargos = new();

    public Guid CargoId(NivelHierarquico nivel) => _cargos[nivel];

    public async Task InitializeAsync()
    {
        Diretor = await CriarContaAsync(nivel: null, nome: "Diretoria");

        var cargos = await Diretor.Cliente.GetFromJsonAsync<List<CargoDto>>("/api/cargos", Json);
        foreach (var c in cargos!) _cargos[c.Nivel] = c.Id;
    }

    public Task DisposeAsync()
    {
        Fabrica.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>HttpClient sem token.</summary>
    public HttpClient Anonimo() => Fabrica.CreateClient();

    /// <summary>
    /// Cadastra, da o cargo (pelo diretor) e faz login, passando pelo 2FA quando o cargo exige.
    /// <paramref name="nivel"/> nulo = sem cargo (conta como funcionario).
    /// </summary>
    public async Task<Conta> CriarContaAsync(NivelHierarquico? nivel, string? nome = null)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var conta = new Conta
        {
            Email = $"{sufixo}@unite.test",
            Senha = "senha123",
            Nome = nome ?? $"Pessoa {sufixo}",
            Cliente = Fabrica.CreateClient()
        };

        var cadastro = await conta.Cliente.PostAsJsonAsync("/api/auth/registrar",
            new RegistrarDto { Email = conta.Email, Senha = conta.Senha, NomeCompleto = conta.Nome });
        cadastro.EnsureSuccessStatusCode();

        // Primeiro login so para descobrir o id (a primeira conta da base ja e Diretor e passa pelo 2FA)...
        conta.Id = await LoginAsync(conta);

        if (nivel is NivelHierarquico n && Diretor is not null)
        {
            var atribuicao = await Diretor.Cliente.PutAsJsonAsync(
                $"/api/usuarios/{conta.Id}/cargo", new AtribuirCargoDto { CargoId = CargoId(n) });
            atribuicao.EnsureSuccessStatusCode();

            // ...e o segundo ja com o cargo, que pode exigir o codigo do Authenticator.
            await LoginAsync(conta);
        }

        return conta;
    }

    /// <summary>Faz o login completo (senha e, se preciso, codigo TOTP), guarda o token no cliente e devolve o id.</summary>
    public async Task<Guid> LoginAsync(Conta conta)
    {
        var usuario = await LoginAsync(conta.Cliente, conta.Email, conta.Senha, conta);
        return usuario.Id;
    }

    private async Task<UsuarioDto> LoginAsync(HttpClient cliente, string email, string senha, Conta conta)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = email, Senha = senha });
        resposta.EnsureSuccessStatusCode();
        var json = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);

        if (json.TryGetProperty("token", out _))
            return Autenticar(cliente, json);

        // Segunda etapa: "configurar" traz a chave nova; "codigo" usa a chave que a conta ja tem.
        var etapa = json.GetProperty("etapa").GetString();
        if (etapa == "configurar")
            conta.ChaveTotp = json.GetProperty("chave").GetString();

        if (conta.ChaveTotp is null)
            throw new InvalidOperationException($"Login pediu etapa '{etapa}' mas a conta nao tem chave TOTP.");

        resposta = await cliente.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = email, Senha = senha, Codigo = Totp.Codigo(conta.ChaveTotp) });
        resposta.EnsureSuccessStatusCode();
        json = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);

        return Autenticar(cliente, json);
    }

    private static UsuarioDto Autenticar(HttpClient cliente, JsonElement json)
    {
        var auth = json.Deserialize<AuthRespostaDto>(Json)!;
        cliente.DefaultRequestAuthorization(auth.Token);
        return auth.Usuario;
    }
}
