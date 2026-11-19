import { HubConnectionBuilder } from '@microsoft/signalr'
import type { HubConnection } from '@microsoft/signalr'
import { token, urlHub } from '../api/client'
import type { Mensagem } from '../api/client'

export function conectarChat(aoReceber: (mensagem: Mensagem) => void): HubConnection {
  const conexao = new HubConnectionBuilder()
    .withUrl(urlHub(), { accessTokenFactory: () => token.ler() ?? '' })
    .withAutomaticReconnect()
    .build()

  conexao.on('MensagemRecebida', aoReceber)

  return conexao
}
