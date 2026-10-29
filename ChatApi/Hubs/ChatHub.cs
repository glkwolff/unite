using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChatApi.Hubs;

/// <summary>
/// Sem metodos de proposito: o envio e o POST do SalasController, que valida e avisa
/// por aqui. O navegador so escuta "MensagemRecebida".
/// </summary>
[Authorize]
public class ChatHub : Hub;
