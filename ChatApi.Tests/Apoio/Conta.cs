using System.Net.Http.Headers;

namespace ChatApi.Tests.Apoio;

public class Conta
{
    public Guid Id { get; set; }
    public required string Email { get; init; }
    public required string Senha { get; init; }
    public required string Nome { get; init; }
    public required HttpClient Cliente { get; init; }

    public string? ChaveTotp { get; set; }
}

public static class HttpClientExtensoes
{
    public static void DefaultRequestAuthorization(this HttpClient cliente, string? token) =>
        cliente.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);
}
