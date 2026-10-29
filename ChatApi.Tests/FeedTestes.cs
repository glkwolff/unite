using System.Net;
using ChatApi.Dtos;
using ChatApi.Models;
using ChatApi.Tests.Apoio;

namespace ChatApi.Tests;

/// <summary>Feed de noticias, paginacao e o botao "Ciente".</summary>
public class FeedTestes(Ambiente ambiente) : IClassFixture<Ambiente>
{
    private static PostagemEntradaDto Aviso(string titulo = "Reuniao", bool institucional = false) =>
        new() { Titulo = titulo, Conteudo = "Amanha as 9h.", Institucional = institucional };

    private static async Task<PostagemDto> PublicarAsync(Conta autor, PostagemEntradaDto? aviso = null)
    {
        var resposta = await autor.Cliente.PostAsync("/api/postagens", aviso ?? Aviso());
        resposta.EnsureSuccessStatusCode();
        return (await resposta.LerAsync<PostagemDto>())!;
    }

    [Fact]
    public async Task Funcionario_nao_publica_e_supervisor_publica()
    {
        var funcionario = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario);
        var supervisor = await ambiente.CriarContaAsync(NivelHierarquico.Supervisor);

        var recusa = await funcionario.Cliente.PostAsync("/api/postagens", Aviso());
        Assert.Equal(HttpStatusCode.Forbidden, recusa.StatusCode);

        var postagem = await PublicarAsync(supervisor);
        Assert.Equal(supervisor.Id, postagem.Autor.Id);
        Assert.True(postagem.PodeRemover);
        Assert.False(postagem.PodeMarcarCiente);
    }

    [Fact]
    public async Task Aviso_institucional_exige_gerente()
    {
        var supervisor = await ambiente.CriarContaAsync(NivelHierarquico.Supervisor);
        var gerente = await ambiente.CriarContaAsync(NivelHierarquico.Gerente);

        var recusa = await supervisor.Cliente.PostAsync("/api/postagens", Aviso(institucional: true));
        Assert.Equal(HttpStatusCode.Forbidden, recusa.StatusCode);

        var postagem = await PublicarAsync(gerente, Aviso(institucional: true));
        Assert.True(postagem.Institucional);
    }

    [Fact]
    public async Task Feed_vem_do_mais_novo_ao_mais_antigo_e_pagina_por_cursor()
    {
        var gerente = await ambiente.CriarContaAsync(NivelHierarquico.Gerente);
        var leitor = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario);

        var ids = new List<Guid>();
        for (var i = 1; i <= 5; i++)
        {
            ids.Add((await PublicarAsync(gerente, Aviso($"Aviso {i}"))).Id);
            await Task.Delay(5); // garante PublicadaEm distinto
        }

        var primeira = await leitor.Cliente.GetAsync<PaginaFeedDto>("/api/postagens?limite=2");
        var itens = primeira!.Itens.ToList();
        Assert.True(primeira.TemMais);
        Assert.Equal([ids[4], ids[3]], itens.Select(p => p.Id));

        var cursor = Uri.EscapeDataString(itens[1].PublicadaEm.ToString("O"));
        var segunda = await leitor.Cliente.GetAsync<PaginaFeedDto>($"/api/postagens?limite=2&antes={cursor}");
        Assert.Equal([ids[2], ids[1]], segunda!.Itens.Select(p => p.Id));
    }

    [Fact]
    public async Task Supervisor_remove_so_as_proprias_e_gerente_remove_qualquer_uma()
    {
        var supervisorA = await ambiente.CriarContaAsync(NivelHierarquico.Supervisor);
        var supervisorB = await ambiente.CriarContaAsync(NivelHierarquico.Supervisor);
        var gerente = await ambiente.CriarContaAsync(NivelHierarquico.Gerente);
        var deA = await PublicarAsync(supervisorA);
        var deB = await PublicarAsync(supervisorB);

        Assert.Equal(HttpStatusCode.Forbidden, (await supervisorA.Cliente.DeleteAsync($"/api/postagens/{deB.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await supervisorA.Cliente.DeleteAsync($"/api/postagens/{deA.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await gerente.Cliente.DeleteAsync($"/api/postagens/{deB.Id}")).StatusCode);
    }

    [Fact]
    public async Task Ciente_e_recusado_ao_autor_e_marcar_duas_vezes_nao_duplica()
    {
        var supervisor = await ambiente.CriarContaAsync(NivelHierarquico.Supervisor);
        var leitor = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario);
        var postagem = await PublicarAsync(supervisor);

        var doAutor = await supervisor.Cliente.PostAsync($"/api/postagens/{postagem.Id}/ciente");
        Assert.Equal(HttpStatusCode.BadRequest, doAutor.StatusCode);

        var primeira = await (await leitor.Cliente.PostAsync($"/api/postagens/{postagem.Id}/ciente")).LerAsync<PostagemDto>();
        var segunda = await (await leitor.Cliente.PostAsync($"/api/postagens/{postagem.Id}/ciente")).LerAsync<PostagemDto>();
        Assert.NotNull(primeira!.CienteEm);
        Assert.Equal(primeira.CienteEm, segunda!.CienteEm);

        var ciencias = await supervisor.Cliente.GetAsync<List<CienciaDto>>($"/api/postagens/{postagem.Id}/ciencias");
        Assert.Equal([leitor.Id], ciencias!.Select(c => c.Usuario.Id));
    }

    [Fact]
    public async Task Quem_ve_a_lista_de_ciencias_segue_a_hierarquia()
    {
        var gerente = await ambiente.CriarContaAsync(NivelHierarquico.Gerente);
        var supervisor = await ambiente.CriarContaAsync(NivelHierarquico.Supervisor);
        var outroSupervisor = await ambiente.CriarContaAsync(NivelHierarquico.Supervisor);
        var funcionario = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario);
        var doSupervisor = await PublicarAsync(supervisor);
        var doGerente = await PublicarAsync(gerente);
        await outroSupervisor.Cliente.PostAsync($"/api/postagens/{doSupervisor.Id}/ciente");
        await funcionario.Cliente.PostAsync($"/api/postagens/{doSupervisor.Id}/ciente");

        // Funcionario: nenhuma. Supervisor: so a propria, e nela so enxerga funcionarios.
        Assert.Equal(HttpStatusCode.Forbidden, (await funcionario.Cliente.GetAsync($"/api/postagens/{doSupervisor.Id}/ciencias")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisor.Cliente.GetAsync($"/api/postagens/{doGerente.Id}/ciencias")).StatusCode);
        var vistasPeloSupervisor = await supervisor.Cliente.GetAsync<List<CienciaDto>>($"/api/postagens/{doSupervisor.Id}/ciencias");
        Assert.Equal([funcionario.Id], vistasPeloSupervisor!.Select(c => c.Usuario.Id));

        // Gerente ve todos os leitores da postagem do supervisor; diretor ve qualquer postagem.
        var vistasPeloGerente = await gerente.Cliente.GetAsync<List<CienciaDto>>($"/api/postagens/{doSupervisor.Id}/ciencias");
        Assert.Equal(2, vistasPeloGerente!.Count);
        Assert.Equal(HttpStatusCode.OK, (await ambiente.Diretor.Cliente.GetAsync($"/api/postagens/{doGerente.Id}/ciencias")).StatusCode);
    }
}
