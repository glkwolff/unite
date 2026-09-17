using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChatApi.Hubs;

/// <summary>
/// Canal de tempo real do chat. O hub nao tem metodo nenhum de proposito:
/// quem grava mensagem e o SalasController, por POST, e o broadcast sai de la
/// via IHubContext. Assim validacao e autorizacao vivem num lugar so — aqui o
/// cliente apenas escuta o evento "MensagemRecebida".
/// </summary>
[Authorize]
public class ChatHub : Hub;
