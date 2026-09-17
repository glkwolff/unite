import { useEffect, useState } from 'react'
import { api, urlArquivo, type Cargo, type Pessoa } from '../api/client'
import { useAuth } from '../auth/useAuth'
import { iniciais } from '../lib/iniciais'
import { cargosAtribuiveis, podeMexerNaPessoa } from '../lib/permissoes'

export function Pessoas() {
  const { usuario } = useAuth()
  const [pessoas, setPessoas] = useState<Pessoa[]>([])
  const [cargos, setCargos] = useState<Cargo[]>([])
  const [carregando, setCarregando] = useState(true)
  const [erro, setErro] = useState<string | null>(null)

  async function recarregar() {
    const [p, c] = await Promise.all([
      api.get<Pessoa[]>('/api/usuarios'),
      api.get<Cargo[]>('/api/cargos'),
    ])
    setPessoas(p)
    setCargos(c)
  }

  useEffect(() => {
    recarregar()
      .catch((e) => setErro(e instanceof Error ? e.message : 'Nao foi possivel carregar.'))
      .finally(() => setCarregando(false))
  }, [])

  async function atribuir(pessoaId: string, cargoId: string) {
    setErro(null)
    try {
      await api.put(`/api/usuarios/${pessoaId}/cargo`, { cargoId: cargoId || null })
      await recarregar()
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Nao foi possivel mudar o cargo.')
    }
  }

  if (carregando) {
    return <p className="text-sm text-slate-500">Carregando…</p>
  }

  const disponiveis = cargosAtribuiveis(usuario?.nivel, cargos)

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-unite-900">Pessoas</h1>
        <p className="mt-1 text-slate-500">
          Atribua o cargo de cada pessoa. O cargo define o que ela pode fazer no Unite.
        </p>
      </div>

      {erro && (
        <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</p>
      )}

      <section className="rounded-xl border border-unite-100 bg-white">
        <ul className="divide-y divide-unite-100">
          {pessoas.map((p) => (
            <li key={p.id} className="flex flex-wrap items-center gap-3 px-5 py-3">
              {p.fotoUrl ? (
                <img
                  src={urlArquivo(p.fotoUrl)}
                  alt={p.nomeCompleto}
                  className="size-9 rounded-full object-cover"
                />
              ) : (
                <span className="flex size-9 items-center justify-center rounded-full bg-unite-700 text-sm font-medium text-white">
                  {iniciais(p.nomeCompleto)}
                </span>
              )}

              <div className="min-w-40 flex-1">
                <p className="text-sm font-medium text-unite-900">{p.nomeCompleto}</p>
                <p className="text-xs text-slate-500">{p.equipe ?? 'Sem equipe'}</p>
              </div>

              {podeMexerNaPessoa(usuario?.nivel, p.nivel) ? (
                <select
                  value={p.cargoId ?? ''}
                  onChange={(e) => atribuir(p.id, e.target.value)}
                  className="rounded-md border border-unite-100 px-2 py-1.5 text-sm outline-none focus:border-unite-400"
                >
                  <option value="">Sem cargo</option>
                  {disponiveis.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.nome} ({c.nivel})
                    </option>
                  ))}
                </select>
              ) : (
                /* Um gerente nao mexe em diretor nem em outro gerente. */
                <span
                  className="text-sm text-slate-500"
                  title="Voce nao tem permissao para mudar este cargo"
                >
                  {p.cargo ?? 'Sem cargo'}
                </span>
              )}
            </li>
          ))}
        </ul>
      </section>
    </div>
  )
}
