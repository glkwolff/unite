import { useEffect, useState, type FormEvent } from 'react'
import { api, urlArquivo, type Ciencia, type MembroResumo, type PaginaFeed, type Postagem } from '../api/client'
import { useAuth } from '../auth/useAuth'
import { iniciais } from '../lib/iniciais'
import { podePublicar, podePublicarInstitucional } from '../lib/permissoes'

const LIMITE = 20

// "01/10/2026 às 19:52" — no fuso de quem esta lendo.
const formatoData = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' })
const formatoHora = new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit' })
const formatoCompleto = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'full', timeStyle: 'short' })

function dataEHora(iso: string) {
  const d = new Date(iso)
  return `${formatoData.format(d)} às ${formatoHora.format(d)}`
}

/** "agora", "há 5 min", "há 3 h" — so nas primeiras 24 horas. Depois a data basta. */
function relativo(iso: string) {
  const minutos = Math.floor((Date.now() - new Date(iso).getTime()) / 60_000)
  if (minutos < 1) return 'agora'
  if (minutos < 60) return `há ${minutos} min`
  const horas = Math.floor(minutos / 60)
  return horas < 24 ? `há ${horas} h` : null
}

export function Feed() {
  const { usuario } = useAuth()
  const [postagens, setPostagens] = useState<Postagem[]>([])
  const [temMais, setTemMais] = useState(false)
  const [carregando, setCarregando] = useState(true)
  const [carregandoMais, setCarregandoMais] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  useEffect(() => {
    api
      .get<PaginaFeed>(`/api/postagens?limite=${LIMITE}`)
      .then((pagina) => {
        setPostagens(pagina.itens)
        setTemMais(pagina.temMais)
      })
      .catch((e) => setErro(e instanceof Error ? e.message : 'Nao foi possivel carregar o feed.'))
      .finally(() => setCarregando(false))
  }, [])

  /** Cursor = data da postagem mais antiga que ja esta na tela. */
  async function carregarMais() {
    const ultima = postagens.at(-1)
    if (!ultima) return
    setCarregandoMais(true)
    setErro(null)
    try {
      const pagina = await api.get<PaginaFeed>(
        `/api/postagens?limite=${LIMITE}&antes=${encodeURIComponent(ultima.publicadaEm)}`,
      )
      setPostagens((atuais) => [...atuais, ...pagina.itens])
      setTemMais(pagina.temMais)
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Nao foi possivel carregar mais avisos.')
    } finally {
      setCarregandoMais(false)
    }
  }

  async function remover(postagem: Postagem) {
    if (!confirm(`Remover o aviso "${postagem.titulo}"?`)) return
    setErro(null)
    try {
      await api.delete(`/api/postagens/${postagem.id}`)
      setPostagens((atuais) => atuais.filter((p) => p.id !== postagem.id))
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Nao foi possivel remover o aviso.')
    }
  }

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-unite-900">Feed de notícias</h1>
        <p className="mt-1 text-slate-500">Avisos e comunicados da empresa, do mais recente ao mais antigo.</p>
      </div>

      {podePublicar(usuario?.nivel) ? (
        <NovoAviso
          institucionalPermitido={podePublicarInstitucional(usuario?.nivel)}
          aoPublicar={(nova) => setPostagens((atuais) => [nova, ...atuais])}
        />
      ) : (
        <p className="rounded-lg border border-dashed border-unite-100 px-4 py-3 text-sm text-slate-500">
          Avisos são publicados por supervisores, gerentes e diretores.
        </p>
      )}

      {erro && <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</p>}

      {carregando ? (
        <p className="text-sm text-slate-500">Carregando…</p>
      ) : postagens.length === 0 ? (
        <p className="rounded-xl border border-unite-100 bg-white px-5 py-10 text-center text-sm text-slate-400">
          Nenhum aviso publicado ainda.
        </p>
      ) : (
        <ol className="space-y-4">
          {postagens.map((p) => (
            <li key={p.id}>
              <CartaoPostagem
                postagem={p}
                aoRemover={() => remover(p)}
                aoAtualizar={(nova) =>
                  setPostagens((atuais) => atuais.map((x) => (x.id === nova.id ? nova : x)))
                }
              />
            </li>
          ))}
        </ol>
      )}

      {temMais && (
        <div className="text-center">
          <button
            type="button"
            onClick={carregarMais}
            disabled={carregandoMais}
            className="rounded-md border border-unite-100 bg-white px-4 py-2 text-sm text-slate-600 transition hover:bg-unite-50 disabled:opacity-60"
          >
            {carregandoMais ? 'Carregando…' : 'Carregar avisos anteriores'}
          </button>
        </div>
      )}
    </div>
  )
}

// -------------------------------------------------------------- publicar

function NovoAviso({
  institucionalPermitido,
  aoPublicar,
}: {
  /** Aviso institucional: so gerente e diretor. Supervisor nem ve a opcao. */
  institucionalPermitido: boolean
  aoPublicar: (postagem: Postagem) => void
}) {
  const [titulo, setTitulo] = useState('')
  const [conteudo, setConteudo] = useState('')
  const [institucional, setInstitucional] = useState(false)
  const [enviando, setEnviando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  async function publicar(evento: FormEvent) {
    evento.preventDefault()
    setEnviando(true)
    setErro(null)
    try {
      const nova = await api.post<Postagem>('/api/postagens', { titulo, conteudo, institucional })
      aoPublicar(nova)
      setTitulo('')
      setConteudo('')
      setInstitucional(false)
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Nao foi possivel publicar.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <form onSubmit={publicar} className="rounded-xl border border-unite-100 bg-white p-5">
      <h2 className="font-medium text-unite-900">Novo aviso</h2>

      <input
        required
        maxLength={160}
        value={titulo}
        onChange={(e) => setTitulo(e.target.value)}
        placeholder="Título"
        className="mt-3 w-full rounded-md border border-unite-100 px-3 py-2 font-medium outline-none focus:border-unite-400"
      />

      <textarea
        required
        maxLength={5000}
        rows={4}
        value={conteudo}
        onChange={(e) => setConteudo(e.target.value)}
        placeholder="Escreva o aviso…"
        className="mt-2 w-full resize-y rounded-md border border-unite-100 px-3 py-2 text-sm outline-none focus:border-unite-400"
      />

      {erro && <p className="mt-2 rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</p>}

      <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
        {institucionalPermitido ? (
          <label className="flex items-center gap-2 text-sm text-slate-600">
            <input
              type="checkbox"
              checked={institucional}
              onChange={(e) => setInstitucional(e.target.checked)}
              className="size-4 accent-unite-700"
            />
            Aviso institucional (em nome da empresa)
          </label>
        ) : (
          <span />
        )}

        <button
          type="submit"
          disabled={enviando || !titulo.trim() || !conteudo.trim()}
          className="rounded-md bg-unite-700 px-4 py-2 text-sm font-medium text-white transition hover:bg-unite-900 disabled:opacity-60"
        >
          {enviando ? 'Publicando…' : 'Publicar'}
        </button>
      </div>
    </form>
  )
}

// ----------------------------------------------------------------- cartao

function CartaoPostagem({
  postagem,
  aoRemover,
  aoAtualizar,
}: {
  postagem: Postagem
  aoRemover: () => void
  /** Troca a postagem na lista depois de marcar "Ciente". */
  aoAtualizar: (postagem: Postagem) => void
}) {
  const recente = relativo(postagem.publicadaEm)
  const [marcando, setMarcando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [listaAberta, setListaAberta] = useState(false)

  async function marcarCiente() {
    setMarcando(true)
    setErro(null)
    try {
      aoAtualizar(await api.post<Postagem>(`/api/postagens/${postagem.id}/ciente`, {}))
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Nao foi possivel marcar ciente.')
    } finally {
      setMarcando(false)
    }
  }

  const temRodape = postagem.podeMarcarCiente || postagem.podeVerCiencias || postagem.podeRemover

  return (
    <article
      className={`rounded-xl border bg-white p-5 ${
        postagem.institucional ? 'border-unite-400 border-l-4' : 'border-unite-100'
      }`}
    >
      <header className="flex items-start gap-3">
        <Avatar pessoa={postagem.autor} />

        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-medium text-unite-900">{postagem.autor.nomeCompleto}</p>
          <p className="text-xs text-slate-500">
            {postagem.autor.cargo && <>{postagem.autor.cargo} · </>}
            <time
              dateTime={postagem.publicadaEm}
              title={formatoCompleto.format(new Date(postagem.publicadaEm))}
            >
              {dataEHora(postagem.publicadaEm)}
            </time>
            {recente && <span className="text-slate-400"> ({recente})</span>}
          </p>
        </div>

        {postagem.institucional && (
          <span className="shrink-0 rounded-full bg-unite-900 px-2.5 py-0.5 text-xs font-medium text-white">
            Institucional
          </span>
        )}
      </header>

      <h3 className="mt-3 font-semibold text-unite-900">{postagem.titulo}</h3>
      <p className="mt-1 whitespace-pre-line break-words text-sm text-slate-700">{postagem.conteudo}</p>

      {temRodape && (
        <footer className="mt-4 flex flex-wrap items-center gap-3 border-t border-unite-100 pt-3">
          {postagem.podeMarcarCiente &&
            (postagem.cienteEm ? (
              <span className="flex items-center gap-1.5 rounded-md bg-emerald-50 px-3 py-1.5 text-sm font-medium text-emerald-700">
                <span aria-hidden>✓</span> Ciente
                <span className="font-normal text-emerald-600">· {dataEHora(postagem.cienteEm)}</span>
              </span>
            ) : (
              <button
                type="button"
                onClick={marcarCiente}
                disabled={marcando}
                className="rounded-md bg-unite-700 px-3 py-1.5 text-sm font-medium text-white transition hover:bg-unite-900 disabled:opacity-60"
              >
                {marcando ? 'Marcando…' : 'Ciente'}
              </button>
            ))}

          {postagem.podeVerCiencias && (
            <button
              type="button"
              onClick={() => setListaAberta((aberta) => !aberta)}
              aria-expanded={listaAberta}
              className="text-sm text-unite-700 hover:underline"
            >
              {postagem.totalCiencias === 1 ? '1 pessoa ciente' : `${postagem.totalCiencias ?? 0} pessoas cientes`}
              {listaAberta ? ' ▴' : ' ▾'}
            </button>
          )}

          {postagem.podeRemover && (
            <button type="button" onClick={aoRemover} className="ml-auto text-xs text-red-600 hover:underline">
              Remover
            </button>
          )}
        </footer>
      )}

      {erro && <p className="mt-2 rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</p>}

      {/* key: recarrega a lista quando o total muda (alguem marcou enquanto estava aberta) */}
      {listaAberta && <ListaCiencias key={postagem.totalCiencias ?? 0} postagemId={postagem.id} />}
    </article>
  )
}

/** Quem marcou "Ciente", na ordem em que marcou. O servidor ja aplica o recorte
 * por nivel — o supervisor, por exemplo, so recebe os funcionarios. */
function ListaCiencias({ postagemId }: { postagemId: string }) {
  const [ciencias, setCiencias] = useState<Ciencia[] | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  useEffect(() => {
    api
      .get<Ciencia[]>(`/api/postagens/${postagemId}/ciencias`)
      .then(setCiencias)
      .catch((e) => setErro(e instanceof Error ? e.message : 'Nao foi possivel carregar a lista.'))
  }, [postagemId])

  if (erro) return <p className="mt-3 rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</p>
  if (!ciencias) return <p className="mt-3 text-sm text-slate-500">Carregando…</p>
  if (ciencias.length === 0) return <p className="mt-3 text-sm text-slate-400">Ninguém marcou ciente ainda.</p>

  return (
    <ul className="mt-3 divide-y divide-unite-100 rounded-lg bg-unite-50/60">
      {ciencias.map((c) => (
        <li key={c.usuario.id} className="flex items-center gap-3 px-3 py-2">
          <Avatar pessoa={c.usuario} tamanho="pequeno" />
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm text-unite-900">{c.usuario.nomeCompleto}</p>
            {c.usuario.cargo && <p className="text-xs text-slate-500">{c.usuario.cargo}</p>}
          </div>
          <time dateTime={c.confirmadaEm} className="shrink-0 text-xs text-slate-500">
            {dataEHora(c.confirmadaEm)}
          </time>
        </li>
      ))}
    </ul>
  )
}

function Avatar({ pessoa, tamanho = 'normal' }: { pessoa: MembroResumo; tamanho?: 'normal' | 'pequeno' }) {
  const medida = tamanho === 'pequeno' ? 'size-7 text-xs' : 'size-10 text-sm'
  return pessoa.fotoUrl ? (
    <img
      src={urlArquivo(pessoa.fotoUrl)}
      alt={pessoa.nomeCompleto}
      className={`${medida} shrink-0 rounded-full object-cover`}
    />
  ) : (
    <span className={`flex ${medida} shrink-0 items-center justify-center rounded-full bg-unite-700 font-medium text-white`}>
      {iniciais(pessoa.nomeCompleto)}
    </span>
  )
}
