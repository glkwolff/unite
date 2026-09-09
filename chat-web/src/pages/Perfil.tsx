import { useRef, useState, type FormEvent } from 'react'
import { api, urlArquivo, type Usuario } from '../api/client'
import { useAuth } from '../auth/useAuth'
import { Campo } from '../components/Campo'
import { iniciais } from '../lib/iniciais'

export function Perfil() {
  const { usuario, atualizarUsuario } = useAuth()
  const inputFotoRef = useRef<HTMLInputElement>(null)

  const [nomeCompleto, setNomeCompleto] = useState(usuario?.nomeCompleto ?? '')
  const [editando, setEditando] = useState(false)
  const [salvando, setSalvando] = useState(false)
  const [enviandoFoto, setEnviandoFoto] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [mensagem, setMensagem] = useState<string | null>(null)

  if (!usuario) return null

  async function salvarNome(evento: FormEvent) {
    evento.preventDefault()
    setErro(null)
    setMensagem(null)
    setSalvando(true)
    try {
      const atualizado = await api.put<Usuario>('/api/perfil', { nomeCompleto })
      atualizarUsuario(atualizado)
      setMensagem('Nome atualizado.')
      setEditando(false)
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Nao foi possivel salvar.')
    } finally {
      setSalvando(false)
    }
  }

  async function trocarFoto(evento: React.ChangeEvent<HTMLInputElement>) {
    const arquivo = evento.target.files?.[0]
    evento.target.value = '' // permite escolher o mesmo arquivo de novo depois
    if (!arquivo) return

    setErro(null)
    setMensagem(null)
    setEnviandoFoto(true)
    try {
      const atualizado = await api.upload<Usuario>('/api/perfil/foto', arquivo)
      atualizarUsuario(atualizado)
      setMensagem('Foto atualizada.')
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Nao foi possivel enviar a foto.')
    } finally {
      setEnviandoFoto(false)
    }
  }

  async function removerFoto() {
    setErro(null)
    setMensagem(null)
    try {
      const atualizado = await api.delete<Usuario>('/api/perfil/foto')
      atualizarUsuario(atualizado)
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Nao foi possivel remover a foto.')
    }
  }

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="text-2xl font-semibold text-unite-900">Meu perfil</h1>
      <p className="mt-1 text-slate-500">Seus dados basicos e a foto exibida no Unite.</p>

      {erro && (
        <p className="mt-4 rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</p>
      )}
      {mensagem && (
        <p className="mt-4 rounded-md bg-emerald-50 px-3 py-2 text-sm text-emerald-700">
          {mensagem}
        </p>
      )}

      <div className="mt-6 flex items-center gap-5 rounded-xl border border-unite-100 bg-white p-6">
        {usuario.fotoUrl ? (
          <img
            src={urlArquivo(usuario.fotoUrl)}
            alt={usuario.nomeCompleto}
            className="size-20 rounded-full object-cover"
          />
        ) : (
          <span className="flex size-20 items-center justify-center rounded-full bg-unite-700 text-xl font-medium text-white">
            {iniciais(usuario.nomeCompleto)}
          </span>
        )}

        <div>
          <input
            ref={inputFotoRef}
            type="file"
            accept="image/png,image/jpeg,image/webp"
            className="hidden"
            onChange={trocarFoto}
          />
          <div className="flex gap-2">
            <button
              type="button"
              onClick={() => inputFotoRef.current?.click()}
              disabled={enviandoFoto}
              className="rounded-md bg-unite-700 px-3 py-1.5 text-sm font-medium text-white transition hover:bg-unite-900 disabled:opacity-60"
            >
              {enviandoFoto ? 'Enviando…' : 'Trocar foto'}
            </button>
            {usuario.fotoUrl && (
              <button
                type="button"
                onClick={removerFoto}
                className="rounded-md border border-unite-100 px-3 py-1.5 text-sm text-slate-600 transition hover:bg-unite-50"
              >
                Remover
              </button>
            )}
          </div>
          <p className="mt-1 text-xs text-slate-400">JPG, PNG ou WEBP, ate 5 MB.</p>
        </div>
      </div>

      <form
        onSubmit={salvarNome}
        className="mt-4 rounded-xl border border-unite-100 bg-white p-6"
      >
        <div className="flex items-center justify-between">
          <h2 className="font-medium text-unite-900">Dados</h2>
          {!editando && (
            <button
              type="button"
              onClick={() => setEditando(true)}
              className="text-sm font-medium text-unite-700 hover:underline"
            >
              Editar
            </button>
          )}
        </div>

        {editando ? (
          <>
            <div className="mt-4">
              <Campo
                rotulo="Nome completo"
                required
                maxLength={120}
                value={nomeCompleto}
                onChange={(e) => setNomeCompleto(e.target.value)}
              />
            </div>
            <div className="flex gap-2">
              <button
                type="submit"
                disabled={salvando}
                className="rounded-md bg-unite-700 px-4 py-2 text-sm font-medium text-white transition hover:bg-unite-900 disabled:opacity-60"
              >
                {salvando ? 'Salvando…' : 'Salvar'}
              </button>
              <button
                type="button"
                onClick={() => {
                  setEditando(false)
                  setNomeCompleto(usuario.nomeCompleto)
                }}
                className="rounded-md border border-unite-100 px-4 py-2 text-sm text-slate-600 transition hover:bg-unite-50"
              >
                Cancelar
              </button>
            </div>
          </>
        ) : (
          <dl className="mt-4 space-y-3 text-sm">
            <div className="flex justify-between">
              <dt className="text-slate-500">Nome</dt>
              <dd className="font-medium text-unite-900">{usuario.nomeCompleto}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-slate-500">E-mail</dt>
              <dd className="font-medium text-unite-900">{usuario.email}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-slate-500">Cargo</dt>
              <dd className="font-medium text-unite-900">{usuario.cargo ?? 'Sem cargo definido'}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-slate-500">Equipe</dt>
              <dd className="font-medium text-unite-900">{usuario.equipe ?? 'Sem equipe definida'}</dd>
            </div>
          </dl>
        )}
      </form>

      <p className="mt-3 text-xs text-slate-400">
        Cargo e equipe sao atribuidos na tela de Equipes por quem administra a estrutura
        organizacional.
      </p>
    </div>
  )
}
