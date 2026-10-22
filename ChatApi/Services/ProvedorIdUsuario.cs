using Microsoft.AspNetCore.SignalR;

namespace ChatApi.Services;

/// <summary>
/// Diz ao SignalR que o id do usuario e a claim "sub" (o padrao procuraria NameIdentifier).
/// E o que faz o Clients.Users(...) achar a pessoa.
/// </summary>
public class ProvedorIdUsuario : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst("sub")?.Value;
}
