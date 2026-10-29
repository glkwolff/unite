using System.Net;
using System.Text.Json;
using ChatApi.Dtos;
using ChatApi.Models;
using ChatApi.Tests.Apoio;

namespace ChatApi.Tests;

/// <summary>Cadastro, login, JWT e a verificacao em duas etapas.</summary>
public class AutenticacaoTestes(Ambiente ambiente) : IClassFixture<Ambiente>
{
    [Fact]
    public async Task Primeira_conta_da_base_vira_diretor_e_o_2fa_e_obrigatorio()
    {
        var eu = await ambiente.Diretor.Cliente.GetAsync<UsuarioDto>("/api/auth/eu");

        Assert.Equal(NivelHierarquico.Diretor, eu!.Nivel);
        Assert.True(eu.DoisFatoresObrigatorio);
        Assert.True(eu.DoisFatoresAtivo);
    }

    [Fact]
    public async Task Demais_contas_entram_sem_cargo_e_sem_2fa()
    {
        var conta = await ambiente.CriarContaAsync(nivel: null);

        var eu = await conta.Cliente.GetAsync<UsuarioDto>("/api/auth/eu");

        Assert.Equal(conta.Email, eu!.Email);
        Assert.Null(eu.Cargo);
        Assert.False(eu.DoisFatoresObrigatorio);
    }

    [Fact]
    public async Task Senha_errada_e_rota_sem_token_devolvem_401()
    {
        var conta = await ambiente.CriarContaAsync(nivel: null);
        var anonimo = ambiente.Anonimo();

        var login = await anonimo.PostAsync("/api/auth/login", new LoginDto { Email = conta.Email, Senha = "errada999" });
        var semToken = await anonimo.GetAsync("/api/auth/eu");

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, semToken.StatusCode);
    }

    // ---- 2fa

    [Fact]
    public async Task Gerente_sem_2fa_recebe_o_qr_code_no_login_e_entra_com_o_codigo()
    {
        var gerente = await ambiente.CriarContaAsync(nivel: null);
        await ambiente.Diretor.Cliente.PutAsync($"/api/usuarios/{gerente.Id}/cargo",
            new AtribuirCargoDto { CargoId = ambiente.CargoId(NivelHierarquico.Gerente) });

        var anonimo = ambiente.Anonimo();
        var primeira = await anonimo.PostAsync("/api/auth/login", new LoginDto { Email = gerente.Email, Senha = gerente.Senha });
        var etapa = await primeira.LerAsync<SegundaEtapaDto>();

        Assert.Equal("configurar", etapa!.Etapa);
        Assert.StartsWith("otpauth://totp/Unite:", etapa.Uri);

        var segunda = await anonimo.PostAsync("/api/auth/login",
            new LoginDto { Email = gerente.Email, Senha = gerente.Senha, Codigo = Totp.Codigo(etapa.Chave!) });
        var auth = await segunda.LerAsync<AuthRespostaDto>();

        Assert.False(string.IsNullOrEmpty(auth!.Token));
        Assert.True(auth.Usuario.DoisFatoresAtivo);
    }

    [Fact]
    public async Task Com_2fa_ativo_o_login_sem_codigo_pede_o_codigo_e_nao_devolve_token()
    {
        var gerente = await ambiente.CriarContaAsync(NivelHierarquico.Gerente);

        var resposta = await ambiente.Anonimo().PostAsync("/api/auth/login",
            new LoginDto { Email = gerente.Email, Senha = gerente.Senha });
        var json = await resposta.LerAsync<JsonElement>();

        Assert.Equal("codigo", json.GetProperty("etapa").GetString());
        Assert.False(json.TryGetProperty("token", out _));
    }

    [Fact]
    public async Task Codigo_errado_devolve_401()
    {
        var gerente = await ambiente.CriarContaAsync(NivelHierarquico.Gerente);

        var resposta = await ambiente.Anonimo().PostAsync("/api/auth/login",
            new LoginDto { Email = gerente.Email, Senha = gerente.Senha, Codigo = Totp.CodigoErrado(gerente.ChaveTotp!) });

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal("Codigo invalido.", await resposta.ErroAsync());
    }

    [Fact]
    public async Task Funcionario_ativa_e_desativa_pelo_perfil_e_diretor_nao_desativa()
    {
        var pessoa = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario);

        var chave = await (await pessoa.Cliente.PostAsync("/api/auth/2fa/configurar")).LerAsync<ChaveAuthenticatorDto>();
        var ativar = await pessoa.Cliente.PostAsync("/api/auth/2fa/ativar", new CodigoDto { Codigo = Totp.Codigo(chave!.Chave) });
        Assert.True((await ativar.LerAsync<UsuarioDto>())!.DoisFatoresAtivo);

        var desativar = await pessoa.Cliente.PostAsync("/api/auth/2fa/desativar", new CodigoDto { Codigo = Totp.Codigo(chave.Chave) });
        Assert.False((await desativar.LerAsync<UsuarioDto>())!.DoisFatoresAtivo);

        var diretor = await ambiente.Diretor.Cliente.PostAsync("/api/auth/2fa/desativar",
            new CodigoDto { Codigo = Totp.Codigo(ambiente.Diretor.ChaveTotp!) });
        Assert.Equal(HttpStatusCode.Forbidden, diretor.StatusCode);
    }
}
