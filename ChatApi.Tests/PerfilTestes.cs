using System.Net;
using System.Net.Http.Headers;
using ChatApi.Dtos;
using ChatApi.Models;
using ChatApi.Tests.Apoio;

namespace ChatApi.Tests;

public class PerfilTestes(Ambiente ambiente) : IClassFixture<Ambiente>
{
    // Menor PNG valido (1x1). A API confere extensao e tamanho, nao o conteudo.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    [Fact]
    public async Task Atualiza_o_nome_e_o_eu_reflete()
    {
        var pessoa = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario);

        var resposta = await pessoa.Cliente.PutAsync("/api/perfil", new AtualizarPerfilDto { NomeCompleto = "Nome Novo" });

        Assert.Equal("Nome Novo", (await resposta.LerAsync<UsuarioDto>())!.NomeCompleto);
        Assert.Equal("Nome Novo", (await pessoa.Cliente.GetAsync<UsuarioDto>("/api/auth/eu"))!.NomeCompleto);
    }

    [Fact]
    public async Task Foto_e_salva_servida_como_estatico_e_removida()
    {
        var pessoa = await ambiente.CriarContaAsync(NivelHierarquico.Funcionario);
        var conteudo = new ByteArrayContent(Png);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var arquivo = new MultipartFormDataContent { { conteudo, "arquivo", "eu.png" } };

        var envio = await pessoa.Cliente.PostAsync("/api/perfil/foto", arquivo);
        var perfil = await envio.LerAsync<UsuarioDto>();
        Assert.Equal($"/uploads/perfis/{pessoa.Id}.png", perfil!.FotoUrl);

        var servida = await ambiente.Anonimo().GetAsync(perfil.FotoUrl);
        Assert.Equal(Png, await servida.Content.ReadAsByteArrayAsync());

        var remocao = await pessoa.Cliente.DeleteAsync("/api/perfil/foto");
        Assert.Null((await remocao.LerAsync<UsuarioDto>())!.FotoUrl);
        Assert.Equal(HttpStatusCode.NotFound, (await ambiente.Anonimo().GetAsync(perfil.FotoUrl)).StatusCode);
    }
}
