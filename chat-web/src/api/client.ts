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
  doisFatoresAtivo: boolean;
  doisFatoresObrigatorio: boolean;
}

export interface AuthResposta {
  token: string;
  expiraEm: string;
  usuario: Usuario;
}

export interface SegundaEtapa {
  etapa: "codigo" | "configurar";
  chave: string | null;
  uri: string | null;
}

export interface ChaveAuthenticator {
  chave: string;
  uri: string;
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
  lideranca: MembroResumo[];
  equipes: Equipe[];
  semEquipe: MembroResumo[];
}

export type TipoSala = "Privada" | "Grupo";

export interface SalaResumo {
  id: string;
  tipo: TipoSala;
  titulo: string;
  outro: MembroResumo | null;
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

export interface PaginaMensagens {
  itens: Mensagem[];
  temMais: boolean;
}

export interface Postagem {
  id: string;
  titulo: string;
  conteudo: string;
  institucional: boolean;
  publicadaEm: string;
  autor: MembroResumo;
  podeRemover: boolean;
  cienteEm: string | null;
  podeMarcarCiente: boolean;
  podeVerCiencias: boolean;
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

export function urlHub() {
  return `${BASE}/chat`;
}

export function urlArquivo(caminho: string) {
  return caminho.startsWith("http") ? caminho : `${BASE}${caminho}`;
}
