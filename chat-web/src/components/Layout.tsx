import type { ReactNode } from 'react'
import { NavLink } from 'react-router-dom'
import { urlArquivo } from '../api/client'
import { useAuth } from '../auth/useAuth'
import { iniciais } from '../lib/iniciais'

const NAVEGACAO = [
  { rotulo: 'Feed', caminho: '/', disponivel: false },
  { rotulo: 'Chat', caminho: '/chat', disponivel: false },
  { rotulo: 'Equipes', caminho: '/equipes', disponivel: true },
  { rotulo: 'Perfil', caminho: '/perfil', disponivel: true },
]

export function Layout({ children }: { children: ReactNode }) {
  const { usuario, sair } = useAuth()

  return (
    <div className="flex h-full">
      <aside className="hidden w-56 shrink-0 flex-col bg-unite-900 text-unite-100 md:flex">
        <div className="px-6 py-5 text-xl font-semibold tracking-tight text-white">Unite</div>

        <nav className="flex-1 px-3">
          {NAVEGACAO.map((item) =>
            item.disponivel ? (
              <NavLink
                key={item.rotulo}
                to={item.caminho}
                className={({ isActive }) =>
                  `mb-1 block rounded-md px-3 py-2 text-sm transition hover:bg-unite-700 ${
                    isActive ? 'bg-unite-700 font-medium' : ''
                  }`
                }
              >
                {item.rotulo}
              </NavLink>
            ) : (
              <button
                key={item.rotulo}
                type="button"
                disabled
                className="mb-1 w-full cursor-not-allowed rounded-md px-3 py-2 text-left text-sm opacity-40"
              >
                {item.rotulo}
              </button>
            ),
          )}
        </nav>

        <p className="px-6 py-4 text-xs text-unite-400">Feed e Chat chegam a partir de 01/10</p>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex items-center justify-between border-b border-unite-100 bg-white px-6 py-3">
          <span className="text-sm text-slate-500">
            {usuario?.equipe ?? 'Sem equipe'} · {usuario?.cargo ?? 'Sem cargo'}
          </span>

          <div className="flex items-center gap-3">
            <NavLink to="/perfil" title="Ver perfil">
              {usuario?.fotoUrl ? (
                <img
                  src={urlArquivo(usuario.fotoUrl)}
                  alt={usuario.nomeCompleto}
                  className="size-9 rounded-full object-cover"
                />
              ) : (
                <span className="flex size-9 items-center justify-center rounded-full bg-unite-700 text-sm font-medium text-white">
                  {iniciais(usuario?.nomeCompleto ?? '?')}
                </span>
              )}
            </NavLink>
            <span className="hidden text-sm font-medium sm:inline">{usuario?.nomeCompleto}</span>
            <button
              type="button"
              onClick={sair}
              className="rounded-md border border-unite-100 px-3 py-1.5 text-sm text-slate-600 transition hover:bg-unite-50"
            >
              Sair
            </button>
          </div>
        </header>

        <main className="flex-1 overflow-auto p-6">{children}</main>
      </div>
    </div>
  )
}
