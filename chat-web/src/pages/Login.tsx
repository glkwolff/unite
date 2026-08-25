import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { Campo } from '../components/Campo'

export function Login() {
  const { entrar } = useAuth()
  const navegar = useNavigate()

  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault()
    setErro(null)
    setEnviando(true)
    try {
      await entrar(email, senha)
      navegar('/', { replace: true })
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Nao foi possivel entrar.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <div className="flex h-full items-center justify-center p-6">
      <form
        onSubmit={aoEnviar}
        className="w-full max-w-sm rounded-xl border border-unite-100 bg-white p-8 shadow-sm"
      >
        <h1 className="text-2xl font-semibold text-unite-900">Unite</h1>
        <p className="mt-1 mb-6 text-sm text-slate-500">Entre com sua conta corporativa.</p>

        <Campo
          rotulo="E-mail"
          type="email"
          required
          value={email}
          onChange={(e) => setEmail(e.target.value)}
        />

        <Campo
          rotulo="Senha"
          type="password"
          required
          value={senha}
          onChange={(e) => setSenha(e.target.value)}
        />

        {erro && <p className="mb-4 rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</p>}

        <button
          type="submit"
          disabled={enviando}
          className="w-full rounded-md bg-unite-700 py-2.5 font-medium text-white transition hover:bg-unite-900 disabled:opacity-60"
        >
          {enviando ? 'Entrando…' : 'Entrar'}
        </button>

        <p className="mt-5 text-center text-sm text-slate-500">
          Nao tem conta?{' '}
          <Link to="/cadastro" className="font-medium text-unite-700 hover:underline">
            Cadastre-se
          </Link>
        </p>
      </form>
    </div>
  )
}
