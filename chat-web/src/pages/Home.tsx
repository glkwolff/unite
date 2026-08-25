import { useAuth } from '../auth/useAuth'

const ETAPAS = [
  { data: '27/08', titulo: 'Setup do projeto', pronto: true },
  { data: '03/09', titulo: 'Autenticacao base', pronto: true },
  { data: '10/09', titulo: 'Perfil e estrutura organizacional', pronto: false },
  { data: '17/09', titulo: 'Permissoes e hierarquia', pronto: false },
  { data: '01/10', titulo: 'Chat privado', pronto: false },
  { data: '08/10', titulo: 'Chat em grupo e historico', pronto: false },
  { data: '15/10', titulo: 'Feed de noticias', pronto: false },
  { data: '22/10', titulo: 'Botao "Ciente"', pronto: false },
  { data: '29/10', titulo: 'Login com Google', pronto: false },
]

export function Home() {
  const { usuario } = useAuth()

  return (
    <div className="mx-auto max-w-3xl">
      <h1 className="text-2xl font-semibold text-unite-900">
        Ola, {usuario?.nomeCompleto.split(' ')[0]}
      </h1>
      <p className="mt-1 text-slate-500">
        Voce esta autenticado. Os modulos abaixo entram conforme o cronograma do projeto.
      </p>

      <ul className="mt-6 divide-y divide-unite-100 overflow-hidden rounded-xl border border-unite-100 bg-white">
        {ETAPAS.map((etapa) => (
          <li key={etapa.data} className="flex items-center gap-4 px-5 py-3">
            <span className="w-12 shrink-0 text-sm font-medium text-slate-400">{etapa.data}</span>
            <span className="flex-1 text-sm">{etapa.titulo}</span>
            <span
              className={
                etapa.pronto
                  ? 'rounded-full bg-emerald-50 px-2.5 py-0.5 text-xs font-medium text-emerald-700'
                  : 'rounded-full bg-slate-100 px-2.5 py-0.5 text-xs text-slate-500'
              }
            >
              {etapa.pronto ? 'concluido' : 'planejado'}
            </span>
          </li>
        ))}
      </ul>
    </div>
  )
}
