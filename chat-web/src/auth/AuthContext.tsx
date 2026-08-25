import { createContext, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, token, type AuthResposta, type Usuario } from '../api/client'

interface AuthContexto {
  usuario: Usuario | null
  carregando: boolean
  entrar: (email: string, senha: string) => Promise<void>
  cadastrar: (nomeCompleto: string, email: string, senha: string) => Promise<void>
  sair: () => void
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

  const aplicar = useCallback((resposta: AuthResposta) => {
    token.gravar(resposta.token)
    setUsuario(resposta.usuario)
  }, [])

  const entrar = useCallback(
    async (email: string, senha: string) => {
      aplicar(await api.post<AuthResposta>('/api/auth/login', { email, senha }))
    },
    [aplicar],
  )

  const cadastrar = useCallback(
    async (nomeCompleto: string, email: string, senha: string) => {
      aplicar(await api.post<AuthResposta>('/api/auth/registrar', { nomeCompleto, email, senha }))
    },
    [aplicar],
  )

  const sair = useCallback(() => {
    token.limpar()
    setUsuario(null)
  }, [])

  const valor = useMemo(
    () => ({ usuario, carregando, entrar, cadastrar, sair }),
    [usuario, carregando, entrar, cadastrar, sair],
  )

  return <ContextoAuth.Provider value={valor}>{children}</ContextoAuth.Provider>
}
