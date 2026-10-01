using Microsoft.AspNetCore.SignalR;

namespace ChatApi.Services;

/// <summary>
/// Diz ao SignalR qual claim identifica o usuario de uma conexao, o que permite
/// enderecar mensagens com Clients.Users(...) sem gerenciar grupos na mao.
/// O provedor padrao procura ClaimTypes.NameIdentifier; como o Program.cs
/// desliga o MapInboundClaims, o id continua na claim "sub" original.
/// </summary>
public class ProvedorIdUsuario : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst("sub")?.Value;
}
