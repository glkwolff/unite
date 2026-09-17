import { HubConnectionBuilder } from '@microsoft/signalr'
import type { HubConnection } from '@microsoft/signalr'
import { token, urlHub } from '../api/client'
import type { Mensagem } from '../api/client'

/**
 * Conexao de leitura do chat: o envio continua sendo um POST comum, aqui so
 * chegam as mensagens. O token vai pelo accessTokenFactory porque o WebSocket
 * nao manda header Authorization — a API le o access_token da query string.
 */
export function conectarChat(aoReceber: (mensagem: Mensagem) => void): HubConnection {
  const conexao = new HubConnectionBuilder()
    .withUrl(urlHub(), { accessTokenFactory: () => token.ler() ?? '' })
    .withAutomaticReconnect()
    .build()

  conexao.on('MensagemRecebida', aoReceber)

  return conexao
}
