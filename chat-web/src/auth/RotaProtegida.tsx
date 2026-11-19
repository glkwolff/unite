import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import type { Nivel } from '../api/client'
import { useAuth } from './useAuth'

const ORDEM: Nivel[] = ['Diretor', 'Gerente', 'Supervisor', 'Funcionario']

interface Props {
  children: ReactNode
  nivelMinimo?: Nivel
}

export function RotaProtegida({ children, nivelMinimo }: Props) {
  const { usuario, carregando } = useAuth()

  if (carregando) {
    return (
      <div className="flex h-full items-center justify-center text-sm text-slate-500">
        Carregando…
      </div>
    )
  }

  if (!usuario) {
    return <Navigate to="/login" replace />
  }

  if (nivelMinimo) {
    const nivel = usuario.nivel
    const alcanca = nivel !== null && ORDEM.indexOf(nivel) <= ORDEM.indexOf(nivelMinimo)
    if (!alcanca) return <Navigate to="/" replace />
  }

  return <>{children}</>
}
