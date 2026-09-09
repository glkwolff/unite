import { createContext, useEffect, useState, type ReactNode } from 'react'
import { api, token, type AuthResposta, type Usuario } from '../api/client'

interface AuthContexto {
  usuario: Usuario | null
  carregando: boolean
  entrar: (email: string, senha: string) => Promise<void>
  cadastrar: (nomeCompleto: string, email: string, senha: string) => Promise<void>
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

  async function entrar(email: string, senha: string) {
    aplicar(await api.post<AuthResposta>('/api/auth/login', { email, senha }))
  }

  async function cadastrar(nomeCompleto: string, email: string, senha: string) {
    aplicar(await api.post<AuthResposta>('/api/auth/registrar', { nomeCompleto, email, senha }))
  }

  function sair() {
    token.limpar()
    setUsuario(null)
  }

  return (
    <ContextoAuth.Provider
      value={{ usuario, carregando, entrar, cadastrar, sair, atualizarUsuario: setUsuario }}
    >
      {children}
    </ContextoAuth.Provider>
  )
}
