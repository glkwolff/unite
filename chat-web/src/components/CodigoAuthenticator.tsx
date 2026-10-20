import { useEffect, useState, type FormEvent } from 'react'
import QRCode from 'qrcode'
import { api, type AuthResposta, type ChaveAuthenticator } from '../api/client'
import { Campo } from './Campo'

interface Props {
  /** Ausente quando quem ativa ja esta logado (pelo perfil). */
  desafio?: string
  /** Preenchida quando o 2FA esta sendo cadastrado: mostra o QR code e ativa. */
  chave: ChaveAuthenticator | null
  aoConcluir: (resposta: AuthResposta) => void
}

/** Pede o codigo de 6 digitos do Google Authenticator; antes, o QR code se for o cadastro. */
export function CodigoAuthenticator({ desafio, chave, aoConcluir }: Props) {
  const [qrCode, setQrCode] = useState<string | null>(null)
  const [codigo, setCodigo] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  useEffect(() => {
    if (!chave) return
    let cancelado = false
    QRCode.toDataURL(chave.uri, { margin: 1, width: 192 }).then((url) => {
      if (!cancelado) setQrCode(url)
    })
    return () => {
      cancelado = true
    }
  }, [chave])

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault()
    setErro(null)
    setEnviando(true)
    try {
      const rota = chave ? '/api/auth/2fa/ativar' : '/api/auth/2fa/verificar'
      aoConcluir(await api.post<AuthResposta>(rota, { desafio, codigo }))
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Nao foi possivel conferir o codigo.')
      setCodigo('')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <form onSubmit={aoEnviar}>
      {chave && (
        <div className="mb-5 text-sm text-slate-600">
          <p>
            Abra o <strong>Google Authenticator</strong>, toque em <strong>+</strong> e leia o
            QR code:
          </p>
          {qrCode && (
            <img src={qrCode} alt="QR code do Authenticator" className="mx-auto my-4 size-48" />
          )}
          <p className="text-xs text-slate-400">Sem camera? Digite a chave no aplicativo:</p>
          <code className="mt-1 block rounded-md bg-unite-50 px-3 py-2 text-xs break-all text-unite-900">
            {chave.chave}
          </code>
        </div>
      )}

      <Campo
        rotulo="Codigo do aplicativo"
        required
        inputMode="numeric"
        autoComplete="one-time-code"
        pattern="[0-9 ]{6,7}"
        maxLength={7}
        placeholder="000 000"
        autoFocus
        value={codigo}
        onChange={(e) => setCodigo(e.target.value)}
      />

      {erro && <p className="mb-4 rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</p>}

      <button
        type="submit"
        disabled={enviando}
        className="w-full rounded-md bg-unite-700 py-2.5 font-medium text-white transition hover:bg-unite-900 disabled:opacity-60"
      >
        {enviando ? 'Conferindo…' : chave ? 'Ativar' : 'Confirmar'}
      </button>
    </form>
  )
}
