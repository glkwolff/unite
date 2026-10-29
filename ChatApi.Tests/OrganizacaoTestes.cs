using System.Net;
using ChatApi.Dtos;
using ChatApi.Models;
using ChatApi.Tests.Apoio;

namespace ChatApi.Tests;

/// <summary>Cargos, equipes, membros e atribuicao de cargo: a tabela de permissoes (RF11).</summary>
public class OrganizacaoTestes(Ambiente ambiente) : IClassFixture<Ambiente>
{
    [Fact]
    public async Task Qualquer_logado_le_cargos_mas_so_o_diretor_escreve()
    {
        var gerente = await ambiente.CriarContaAsync(NivelHierarquico.Gerente);
        var novo = new CargoEntradaDto { Nome = "Analista", Nivel = NivelHierarquico.Funcionario };

        Assert.Equal(HttpStatusCode.OK, (await gerente.Cliente.GetAsync("/api/cargos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await gerente.Cliente.PostAsync("/api/cargos", novo)).StatusCode);

        var criado = await (await ambiente.Diretor.Cliente.PostAsync("/api/cargos", novo)).LerAsync<CargoDto>();
        Assert.Equal("Analista", criado!.Nome);
    }

    [Fact]
    public async Task Cargo_com_pessoas_nao_e_removido()
    {
        var cargo = await (await ambiente.Diretor.Cliente.PostAsync("/api/cargos",
            new CargoEntradaDto { Nome = "Estagiario", Nivel = NivelHierarquico.Funcionario })).LerAsync<CargoDto>();
        var pessoa = await ambiente.CriarContaAsync(nivel: null);
        await ambiente.Diretor.Cliente.PutAsync($"/api/usuarios/{pessoa.Id}/cargo", new AtribuirCargoDto { CargoId = cargo!.Id });

        var remover = await ambiente.Diretor.Cliente.DeleteAsync($"/api/cargos/{cargo.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, remover.StatusCode);
        Assert.Contains("ainda tem pessoas", await remover.ErroAsync());
    }

    [Fact]
    public async Task Ultimo_diretor_nao_perde_o_cargo()
    {
        var resposta = await ambiente.Diretor.Cliente.PutAsync(
            $"/api/usuarios/{ambiente.Diretor.Id}/cargo", new AtribuirCargoDto { CargoId = ambiente.CargoId(NivelHierarquico.Gerente) });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("unico diretor", await resposta.ErroAsync());
    }

    [Fact]
    public async Task Gerente_distribui_ate_supervisor_e_nao_mexe_na_lideranca()
    {
        var gerente = await ambiente.CriarContaAsync(NivelHierarquico.Gerente);
        var outroGerente = await ambiente.CriarContaAsync(NivelHierarquico.Gerente);
        var pessoa = await ambiente.CriarContaAsync(nivel: null);

        var supervisor = await gerente.Cliente.PutAsync($"/api/usuarios/{pessoa.Id}/cargo",
            new AtribuirCargoDto { CargoId = ambiente.CargoId(NivelHierarquico.Supervisor) });
        Assert.Equal(NivelHierarquico.Supervisor, (await supervisor.LerAsync<PessoaDto>())!.Nivel);

        var promoverAGerente = await gerente.Cliente.PutAsync($"/api/usuarios/{pessoa.Id}/cargo",
            new AtribuirCargoDto { CargoId = ambiente.CargoId(NivelHierarquico.Gerente) });
        Assert.Equal(HttpStatusCode.Forbidden, promoverAGerente.StatusCode);

        var rebaixarGerente = await gerente.Cliente.PutAsync($"/api/usuarios/{outroGerente.Id}/cargo",
            new AtribuirCargoDto { CargoId = ambiente.CargoId(NivelHierarquico.Funcionario) });
        Assert.Equal(HttpStatusCode.Forbidden, rebaixarGerente.StatusCode);
    }

    [Fact]
    public async Task Gerente_cria_equipe_e_supervisor_nao()
    {
        var gerente = await ambiente.CriarContaAsync(NivelHierarquico.Gerente);
        var supervisor = await ambiente.CriarContaAsync(NivelHierarquico.Supervisor);

        var recusa = await supervisor.Cliente.PostAsync("/api/equipes", new EquipeEntradaDto { Nome = "Vendas" });
        Assert.Equal(HttpStatusCode.Forbidden, recusa.StatusCode);

        var criada = await gerente.Cliente.PostAsync("/api/equipes", new EquipeEntradaDto { Nome = "Vendas", SupervisorId = supervisor.Id });
        var equipe = await criada.LerAsync<EquipeDto>();
        Assert.Equal("Vendas", equipe!.Nome);
        Assert.Equal(supervisor.Id, equipe.Supervisor!.Id);
    }

    [Fact]
    public async Task Supervisor_gerencia_membros_so_da_propria_equipe()
    {
        var supervisor = await ambiente.CriarContaAsync(NivelHierarquico.Supervisor);
        var membro = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario);
        var minha = await (await ambiente.Diretor.Cliente.PostAsync("/api/equipes",
            new EquipeEntradaDto { Nome = "Minha", SupervisorId = supervisor.Id })).LerAsync<EquipeDto>();
        var outra = await (await ambiente.Diretor.Cliente.PostAsync("/api/equipes",
            new EquipeEntradaDto { Nome = "Outra" })).LerAsync<EquipeDto>();

        var naOutra = await supervisor.Cliente.PostAsync($"/api/equipes/{outra!.Id}/membros", new AdicionarMembroDto { UsuarioId = membro.Id });
        Assert.Equal(HttpStatusCode.Forbidden, naOutra.StatusCode);

        var naMinha = await supervisor.Cliente.PostAsync($"/api/equipes/{minha!.Id}/membros", new AdicionarMembroDto { UsuarioId = membro.Id });
        Assert.Contains((await naMinha.LerAsync<EquipeDto>())!.Membros, m => m.Id == membro.Id);

        var removido = await supervisor.Cliente.DeleteAsync($"/api/equipes/{minha.Id}/membros/{membro.Id}");
        Assert.DoesNotContain((await removido.LerAsync<EquipeDto>())!.Membros, m => m.Id == membro.Id);
    }
}
