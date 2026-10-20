import { createContext, useEffect, useState, type ReactNode } from 'react'
import {
  api,
  token,
  type AuthResposta,
  type ChaveAuthenticator,
  type Desafio,
  type Usuario,
} from '../api/client'

/** O que falta depois da senha. `chave` vem preenchida quando o 2FA ainda vai ser cadastrado. */
export interface SegundaEtapa {
  desafio: string
  chave: ChaveAuthenticator | null
}

interface AuthContexto {
  usuario: Usuario | null
  carregando: boolean
  /** Nulo quando ja entrou; senao, a etapa do codigo que falta. */
  entrar: (email: string, senha: string) => Promise<SegundaEtapa | null>
  cadastrar: (nomeCompleto: string, email: string, senha: string) => Promise<SegundaEtapa | null>
  concluirEntrada: (resposta: AuthResposta) => void
  sair: () => void
  /** Atualiza o usuario em memoria (ex.: apos editar perfil ou trocar foto),
   * sem precisar relogar para o header refletir a mudanca. */
  atualizarUsuario: (usuario: Usuario) => void
}

// eslint-disable-next-line react-refresh/only-export-components
export const ContextoAuth = createContext<AuthContexto | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<Usuario | null>(null)
  const [carregando, setCarregando] = useState(true)

  // Restaura a sessao quando ja existe token guardado.
  useEffect(() => {
    if (!token.ler()) {
      setCarregando(false)
      return
    }

    api
      .get<Usuario>('/api/auth/eu')
      .then(setUsuario)
      .catch(() => token.limpar())
      .finally(() => setCarregando(false))
  }, [])

  function aplicar(resposta: AuthResposta) {
    token.gravar(resposta.token)
    setUsuario(resposta.usuario)
  }

  async function seguir(resposta: AuthResposta | Desafio): Promise<SegundaEtapa | null> {
    if (!('desafio' in resposta)) {
      aplicar(resposta)
      return null
    }

    const chave =
      resposta.etapa === 'configurar'
        ? await api.post<ChaveAuthenticator>('/api/auth/2fa/configurar', {
            desafio: resposta.desafio,
          })
        : null
    return { desafio: resposta.desafio, chave }
  }

  async function entrar(email: string, senha: string) {
    return seguir(await api.post<AuthResposta | Desafio>('/api/auth/login', { email, senha }))
  }

  async function cadastrar(nomeCompleto: string, email: string, senha: string) {
    return seguir(
      await api.post<AuthResposta | Desafio>('/api/auth/registrar', { nomeCompleto, email, senha }),
    )
  }

  function sair() {
    token.limpar()
    setUsuario(null)
  }

  return (
    <ContextoAuth.Provider
      value={{
        usuario,
        carregando,
        entrar,
        cadastrar,
        concluirEntrada: aplicar,
        sair,
        atualizarUsuario: setUsuario,
      }}
    >
      {children}
    </ContextoAuth.Provider>
  )
}
