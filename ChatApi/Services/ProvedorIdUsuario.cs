using Microsoft.AspNetCore.SignalR;

namespace ChatApi.Services;

public class ProvedorIdUsuario : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst("sub")?.Value;
}
