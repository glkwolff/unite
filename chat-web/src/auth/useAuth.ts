import { useContext } from 'react'
import { ContextoAuth } from './AuthContext'

export function useAuth() {
  const contexto = useContext(ContextoAuth)
  if (!contexto) {
    throw new Error('useAuth precisa estar dentro de <AuthProvider>.')
  }
  return contexto
}
