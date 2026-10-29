using System.Net;
using ChatApi.Dtos;
using ChatApi.Models;
using ChatApi.Tests.Apoio;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace ChatApi.Tests;

/// <summary>Conversa privada, canal da equipe, historico paginado e o aviso pelo SignalR.</summary>
public class ChatTestes(Ambiente ambiente) : IClassFixture<Ambiente>
{
    private static async Task<MensagemDto> EnviarAsync(Conta autor, Guid salaId, string texto)
    {
        var resposta = await autor.Cliente.PostAsync($"/api/salas/{salaId}/mensagens", new EnviarMensagemDto { Texto = texto });
        resposta.EnsureSuccessStatusCode();
        return (await resposta.LerAsync<MensagemDto>())!;
    }

    private static async Task<SalaResumoDto> AbrirPrivadaAsync(Conta de, Conta com)
    {
        var resposta = await de.Cliente.PostAsync("/api/salas/privada", new AbrirPrivadaDto { UsuarioId = com.Id });
        resposta.EnsureSuccessStatusCode();
        return (await resposta.LerAsync<SalaResumoDto>())!;
    }

    [Fact]
    public async Task Conversa_privada_e_idempotente_e_o_titulo_e_o_nome_do_outro()
    {
        var ana = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario, nome: "Ana");
        var bia = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario, nome: "Bia");

        var deAna = await AbrirPrivadaAsync(ana, bia);
        var deBia = await AbrirPrivadaAsync(bia, ana);

        Assert.Equal(deAna.Id, deBia.Id);
        Assert.Equal("Bia", deAna.Titulo);
        Assert.Equal("Ana", deBia.Titulo);
    }

    [Fact]
    public async Task Quem_nao_participa_nao_le_nem_escreve_mesmo_sendo_gerente()
    {
        var ana = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario);
        var bia = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario);
        var gerente = await ambiente.CriarContaAsync(NivelHierarquico.Gerente); // chat nao segue hierarquia
        var sala = await AbrirPrivadaAsync(ana, bia);

        var ler = await gerente.Cliente.GetAsync($"/api/salas/{sala.Id}/mensagens");
        var escrever = await gerente.Cliente.PostAsync($"/api/salas/{sala.Id}/mensagens", new EnviarMensagemDto { Texto = "oi" });

        Assert.Equal(HttpStatusCode.Forbidden, ler.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, escrever.StatusCode);
    }

    [Fact]
    public async Task Historico_vem_em_ordem_de_leitura_e_pagina_para_tras()
    {
        var ana = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario);
        var bia = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario);
        var sala = await AbrirPrivadaAsync(ana, bia);
        for (var i = 1; i <= 5; i++)
        {
            await EnviarAsync(i % 2 == 0 ? bia : ana, sala.Id, $"m{i}");
            await Task.Delay(5); // garante EnviadaEm distinto
        }

        var fim = await ana.Cliente.GetAsync<PaginaMensagensDto>($"/api/salas/{sala.Id}/mensagens?limite=2");
        Assert.True(fim!.TemMais);
        Assert.Equal(["m4", "m5"], fim.Itens.Select(m => m.Texto));

        var cursor = Uri.EscapeDataString(fim.Itens.First().EnviadaEm.ToString("O"));
        var anterior = await ana.Cliente.GetAsync<PaginaMensagensDto>($"/api/salas/{sala.Id}/mensagens?limite=2&antesDe={cursor}");
        Assert.Equal(["m2", "m3"], anterior!.Itens.Select(m => m.Texto));
    }

    [Fact]
    public async Task Canal_da_equipe_nasce_no_primeiro_uso_e_participa_quem_esta_na_equipe_agora()
    {
        var ana = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario);
        var bia = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario);
        var deFora = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario);
        var equipe = await (await ambiente.Diretor.Cliente.PostAsync("/api/equipes", new EquipeEntradaDto { Nome = "Suporte" })).LerAsync<EquipeDto>();
        foreach (var m in new[] { ana, bia })
            await ambiente.Diretor.Cliente.PostAsync($"/api/equipes/{equipe!.Id}/membros", new AdicionarMembroDto { UsuarioId = m.Id });

        var canalDeAna = await (await ana.Cliente.PostAsync("/api/salas/equipe")).LerAsync<SalaResumoDto>();
        var canalDeBia = await (await bia.Cliente.PostAsync("/api/salas/equipe")).LerAsync<SalaResumoDto>();
        Assert.Equal(canalDeAna!.Id, canalDeBia!.Id);
        Assert.Equal(TipoSala.Grupo, canalDeAna.Tipo);
        Assert.Equal("Suporte", canalDeAna.Titulo);

        await EnviarAsync(ana, canalDeAna.Id, "bom dia equipe");
        Assert.Equal(HttpStatusCode.Forbidden, (await deFora.Cliente.GetAsync($"/api/salas/{canalDeAna.Id}/mensagens")).StatusCode);

        // Quem entra na equipe passa a ler; quem sai perde o acesso. Sem religar nada.
        await ambiente.Diretor.Cliente.PostAsync($"/api/equipes/{equipe!.Id}/membros", new AdicionarMembroDto { UsuarioId = deFora.Id });
        var historico = await deFora.Cliente.GetAsync<PaginaMensagensDto>($"/api/salas/{canalDeAna.Id}/mensagens");
        Assert.Equal(["bom dia equipe"], historico!.Itens.Select(m => m.Texto));

        await ambiente.Diretor.Cliente.DeleteAsync($"/api/equipes/{equipe.Id}/membros/{bia.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, (await bia.Cliente.GetAsync($"/api/salas/{canalDeAna.Id}/mensagens")).StatusCode);
    }

    [Fact]
    public async Task Participante_recebe_MensagemRecebida_pelo_hub_e_quem_nao_participa_nao_recebe()
    {
        var ana = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario);
        var bia = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario);
        var caio = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario);
        var sala = await AbrirPrivadaAsync(ana, bia);

        await using var conexaoDeBia = Conectar(bia);
        await using var conexaoDeCaio = Conectar(caio);
        var recebidaPorBia = new TaskCompletionSource<MensagemDto>();
        var recebidaPorCaio = new TaskCompletionSource<MensagemDto>();
        conexaoDeBia.On<MensagemDto>("MensagemRecebida", m => recebidaPorBia.TrySetResult(m));
        conexaoDeCaio.On<MensagemDto>("MensagemRecebida", m => recebidaPorCaio.TrySetResult(m));
        await conexaoDeBia.StartAsync();
        await conexaoDeCaio.StartAsync();

        var enviada = await EnviarAsync(ana, sala.Id, "chegou?");

        var recebida = await recebidaPorBia.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(enviada.Id, recebida.Id);
        Assert.Equal(ana.Nome, recebida.AutorNome);

        var caioRecebeu = await Task.WhenAny(recebidaPorCaio.Task, Task.Delay(500)) == recebidaPorCaio.Task;
        Assert.False(caioRecebeu);
    }

    /// <summary>Como o front: token na query string. Long polling porque o TestServer nao fala WebSocket.</summary>
    private HubConnection Conectar(Conta conta)
    {
        var token = conta.Cliente.DefaultRequestHeaders.Authorization!.Parameter;
        return new HubConnectionBuilder()
            .WithUrl($"{ambiente.Fabrica.Server.BaseAddress}chat?access_token={token}", o =>
            {
                o.HttpMessageHandlerFactory = _ => ambiente.Fabrica.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }
}
