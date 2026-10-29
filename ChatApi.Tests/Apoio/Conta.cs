using System.Net.Http.Headers;

namespace ChatApi.Tests.Apoio;

/// <summary>Uma pessoa cadastrada nos testes e o HttpClient ja autenticado como ela.</summary>
public class Conta
{
    /// <summary>Preenchido no primeiro login.</summary>
    public Guid Id { get; set; }
    public required string Email { get; init; }
    public required string Senha { get; init; }
    public required string Nome { get; init; }
    public required HttpClient Cliente { get; init; }

    /// <summary>Chave do Authenticator, quando o 2FA esta ativo (gerente e diretor sempre tem).</summary>
    public string? ChaveTotp { get; set; }
}

public static class HttpClientExtensoes
{
    public static void DefaultRequestAuthorization(this HttpClient cliente, string? token) =>
        cliente.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);
}
