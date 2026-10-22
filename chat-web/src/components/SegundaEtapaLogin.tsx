import { useNavigate } from 'react-router-dom'
import type { SegundaEtapa } from '../api/client'
import { useAuth } from '../auth/useAuth'
import { CodigoAuthenticator } from './CodigoAuthenticator'

interface Props {
  etapa: SegundaEtapa
  /** O login e refeito com a mesma senha, agora junto com o codigo. */
  email: string
  senha: string
  aoVoltar: () => void
}

/** Cartao que substitui o formulario de login depois que a senha confere. */
export function SegundaEtapaLogin({ etapa, email, senha, aoVoltar }: Props) {
  const { entrar } = useAuth()
  const navegar = useNavigate()
  const chave = etapa.chave && etapa.uri ? { chave: etapa.chave, uri: etapa.uri } : null

  return (
    <div className="flex h-full items-center justify-center p-6">
      <div className="w-full max-w-sm rounded-xl border border-unite-100 bg-white p-8 shadow-sm">
        <h1 className="text-2xl font-semibold text-unite-900">Verificacao em duas etapas</h1>
        <p className="mt-1 mb-6 text-sm text-slate-500">
          {chave
            ? 'Seu cargo exige um segundo fator. Configure uma vez e use o codigo em cada login.'
            : 'Digite o codigo que aparece no Google Authenticator.'}
        </p>

        <CodigoAuthenticator
          chave={chave}
          aoEnviar={async (codigo) => {
            if (!(await entrar(email, senha, codigo))) navegar('/', { replace: true })
          }}
        />

        <button
          type="button"
          onClick={aoVoltar}
          className="mt-5 w-full text-center text-sm text-slate-500 hover:underline"
        >
          Voltar
        </button>
      </div>
    </div>
  )
}
