using System.Net.Http.Json;
using System.Text.Json;

namespace ChatApi.Tests.Apoio;

/// <summary>Atalhos para ler o corpo das respostas com as opcoes de JSON da API.</summary>
public static class Http
{
    public static Task<T?> LerAsync<T>(this HttpResponseMessage resposta) =>
        resposta.Content.ReadFromJsonAsync<T>(Ambiente.Json);

    public static Task<T?> GetAsync<T>(this HttpClient cliente, string url) =>
        cliente.GetFromJsonAsync<T>(url, Ambiente.Json);

    public static Task<HttpResponseMessage> PostAsync<T>(this HttpClient cliente, string url, T corpo) =>
        cliente.PostAsJsonAsync(url, corpo, Ambiente.Json);

    public static Task<HttpResponseMessage> PutAsync<T>(this HttpClient cliente, string url, T corpo) =>
        cliente.PutAsJsonAsync(url, corpo, Ambiente.Json);

    /// <summary>POST sem corpo (ex.: /ciente, /2fa/configurar).</summary>
    public static Task<HttpResponseMessage> PostAsync(this HttpClient cliente, string url) =>
        cliente.PostAsync(url, content: null);

    /// <summary>O campo "erro" das respostas de recusa.</summary>
    public static async Task<string?> ErroAsync(this HttpResponseMessage resposta)
    {
        var json = await resposta.Content.ReadFromJsonAsync<JsonElement>(Ambiente.Json);
        return json.TryGetProperty("erro", out var erro) ? erro.GetString() : null;
    }
}
