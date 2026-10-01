const BASE = import.meta.env.VITE_API_URL ?? "http://localhost:5000";
const CHAVE_TOKEN = "unite.token";

export type Nivel = "Diretor" | "Gerente" | "Supervisor" | "Funcionario";

export interface Usuario {
  id: string;
  email: string;
  nomeCompleto: string;
  fotoUrl: string | null;
  cargo: string | null;
  equipe: string | null;
  nivel: Nivel | null;
}

export interface AuthResposta {
  token: string;
  expiraEm: string;
  usuario: Usuario;
}

export interface Cargo {
  id: string;
  nome: string;
  nivel: Nivel;
  totalUsuarios: number;
}

export interface MembroResumo {
  id: string;
  nomeCompleto: string;
  fotoUrl: string | null;
  cargo: string | null;
  nivel: Nivel | null;
}

export interface Equipe {
  id: string;
  nome: string;
  supervisor: MembroResumo | null;
  membros: MembroResumo[];
}

export interface Pessoa extends MembroResumo {
  cargoId: string | null;
  equipeId: string | null;
  equipe: string | null;
}

export interface ArvoreOrganizacional {
  /** Diretoria e gerencia: ficam no topo, fora das equipes. */
  lideranca: MembroResumo[];
  equipes: Equipe[];
  semEquipe: MembroResumo[];
}

export interface SalaResumo {
  id: string;
  /** Na conversa privada o titulo da sala e o nome desta pessoa. */
  outro: MembroResumo;
  ultimaMensagem: string | null;
  ultimaEm: string | null;
}

export interface Mensagem {
  id: string;
  salaId: string;
  autorId: string;
  autorNome: string;
  texto: string;
  enviadaEm: string;
}

export interface Postagem {
  id: string;
  titulo: string;
  conteudo: string;
  institucional: boolean;
  /** ISO 8601 em UTC (com "Z"); o navegador converte para o fuso local. */
  publicadaEm: string;
  autor: MembroResumo;
  /** Calculado pelo servidor: autor da postagem ou gerente para cima. */
  podeRemover: boolean;
  /** Quando o usuario logado marcou "Ciente"; null se ainda nao marcou. */
  cienteEm: string | null;
  /** Falso so para o autor da postagem. */
  podeMarcarCiente: boolean;
  /** Se pode abrir a lista de quem marcou (regra hierarquica no servidor). */
  podeVerCiencias: boolean;
  /** Quantos ele enxerga nessa lista; null quando nao pode ver. */
  totalCiencias: number | null;
}

export interface Ciencia {
  usuario: MembroResumo;
  confirmadaEm: string;
}

export interface PaginaFeed {
  itens: Postagem[];
  temMais: boolean;
}

export class ApiError extends Error {
  // Campo declarado explicitamente: parameter properties nao passam no
  // erasableSyntaxOnly que o template do Vite habilita.
  status: number;

  constructor(status: number, mensagem: string) {
    super(mensagem);
    this.status = status;
  }
}

export const token = {
  ler: () => localStorage.getItem(CHAVE_TOKEN),
  gravar: (valor: string) => localStorage.setItem(CHAVE_TOKEN, valor),
  limpar: () => localStorage.removeItem(CHAVE_TOKEN),
};

async function requisicao<T>(
  caminho: string,
  init?: RequestInit,
  { comCorpoJson = true }: { comCorpoJson?: boolean } = {},
): Promise<T> {
  const atual = token.ler();

  const resposta = await fetch(`${BASE}${caminho}`, {
    ...init,
    headers: {
      // Upload de arquivo define o Content-Type (com boundary) sozinho.
      ...(comCorpoJson ? { "Content-Type": "application/json" } : {}),
      ...(atual ? { Authorization: `Bearer ${atual}` } : {}),
      ...init?.headers,
    },
  });

  if (!resposta.ok) {
    let mensagem = `Erro ${resposta.status}`;
    try {
      const corpo = await resposta.json();
      mensagem = corpo?.erro ?? corpo?.title ?? mensagem;
    } catch {
      // resposta sem corpo JSON: mantem a mensagem padrao
    }
    throw new ApiError(resposta.status, mensagem);
  }

  return resposta.status === 204
    ? (undefined as T)
    : ((await resposta.json()) as T);
}

export const api = {
  get: <T>(caminho: string) => requisicao<T>(caminho),
  post: <T>(caminho: string, corpo: unknown) =>
    requisicao<T>(caminho, { method: "POST", body: JSON.stringify(corpo) }),
  put: <T>(caminho: string, corpo: unknown) =>
    requisicao<T>(caminho, { method: "PUT", body: JSON.stringify(corpo) }),
  delete: <T>(caminho: string) => requisicao<T>(caminho, { method: "DELETE" }),
  upload: <T>(caminho: string, arquivo: File, campo = "arquivo") => {
    const form = new FormData();
    form.append(campo, arquivo);
    return requisicao<T>(
      caminho,
      { method: "POST", body: form },
      { comCorpoJson: false },
    );
  },
};

/** O SignalR monta a conexao fora do fetch e precisa da URL absoluta. */
export function urlHub() {
  return `${BASE}/chat`;
}

export function urlArquivo(caminho: string) {
  return caminho.startsWith("http") ? caminho : `${BASE}${caminho}`;
}
