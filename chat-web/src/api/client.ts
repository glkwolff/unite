const BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5000'
const CHAVE_TOKEN = 'unite.token'

export type Nivel = 'Gerente' | 'Supervisor' | 'Funcionario'

export interface Usuario {
  id: string
  email: string
  nomeCompleto: string
  fotoUrl: string | null
  cargo: string | null
  equipe: string | null
  nivel: Nivel | null
}

export interface AuthResposta {
  token: string
  expiraEm: string
  usuario: Usuario
}

export interface Cargo {
  id: string
  nome: string
  nivel: Nivel
  totalUsuarios: number
}

export interface MembroResumo {
  id: string
  nomeCompleto: string
  fotoUrl: string | null
  cargo: string | null
  nivel: Nivel | null
}

export interface Equipe {
  id: string
  nome: string
  supervisor: MembroResumo | null
  membros: MembroResumo[]
}

export interface ArvoreOrganizacional {
  gerentes: MembroResumo[]
  equipes: Equipe[]
  semEquipe: MembroResumo[]
}

export class ApiError extends Error {
  // Campo declarado explicitamente: parameter properties nao passam no
  // erasableSyntaxOnly que o template do Vite habilita.
  status: number

  constructor(status: number, mensagem: string) {
    super(mensagem)
    this.status = status
  }
}

export const token = {
  ler: () => localStorage.getItem(CHAVE_TOKEN),
  gravar: (valor: string) => localStorage.setItem(CHAVE_TOKEN, valor),
  limpar: () => localStorage.removeItem(CHAVE_TOKEN),
}

async function requisicao<T>(
  caminho: string,
  init?: RequestInit,
  { comCorpoJson = true }: { comCorpoJson?: boolean } = {},
): Promise<T> {
  const atual = token.ler()

  const resposta = await fetch(`${BASE}${caminho}`, {
    ...init,
    headers: {
      // Upload de arquivo define o Content-Type (com boundary) sozinho.
      ...(comCorpoJson ? { 'Content-Type': 'application/json' } : {}),
      ...(atual ? { Authorization: `Bearer ${atual}` } : {}),
      ...init?.headers,
    },
  })

  if (!resposta.ok) {
    let mensagem = `Erro ${resposta.status}`
    try {
      const corpo = await resposta.json()
      mensagem = corpo?.erro ?? corpo?.title ?? mensagem
    } catch {
      // resposta sem corpo JSON: mantem a mensagem padrao
    }
    throw new ApiError(resposta.status, mensagem)
  }

  return resposta.status === 204 ? (undefined as T) : ((await resposta.json()) as T)
}

export const api = {
  get: <T>(caminho: string) => requisicao<T>(caminho),
  post: <T>(caminho: string, corpo: unknown) =>
    requisicao<T>(caminho, { method: 'POST', body: JSON.stringify(corpo) }),
  put: <T>(caminho: string, corpo: unknown) =>
    requisicao<T>(caminho, { method: 'PUT', body: JSON.stringify(corpo) }),
  delete: <T>(caminho: string) => requisicao<T>(caminho, { method: 'DELETE' }),
  upload: <T>(caminho: string, arquivo: File, campo = 'arquivo') => {
    const form = new FormData()
    form.append(campo, arquivo)
    return requisicao<T>(caminho, { method: 'POST', body: form }, { comCorpoJson: false })
  },
}

export function urlArquivo(caminho: string) {
  return caminho.startsWith('http') ? caminho : `${BASE}${caminho}`
}
