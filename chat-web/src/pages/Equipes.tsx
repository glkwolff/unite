import { useEffect, useState, type FormEvent } from "react";
import {
  api,
  type ArvoreOrganizacional,
  type Cargo,
  type Equipe,
  type MembroResumo,
  type Nivel,
} from "../api/client";

const NIVEIS: Nivel[] = ["Diretor", "Gerente", "Supervisor", "Funcionario"];

export function Equipes() {
  const [cargos, setCargos] = useState<Cargo[]>([]);
  const [equipes, setEquipes] = useState<Equipe[]>([]);
  const [usuarios, setUsuarios] = useState<MembroResumo[]>([]);
  const [arvore, setArvore] = useState<ArvoreOrganizacional | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  async function recarregar() {
    const [c, e, u, a] = await Promise.all([
      api.get<Cargo[]>("/api/cargos"),
      api.get<Equipe[]>("/api/equipes"),
      api.get<MembroResumo[]>("/api/usuarios"),
      api.get<ArvoreOrganizacional>("/api/equipes/arvore"),
    ]);
    setCargos(c);
    setEquipes(e);
    setUsuarios(u);
    setArvore(a);
  }

  useEffect(() => {
    recarregar()
      .catch((e) =>
        setErro(e instanceof Error ? e.message : "Nao foi possivel carregar."),
      )
      .finally(() => setCarregando(false));
  }, []);

  async function comAtualizacao(acao: () => Promise<unknown>) {
    setErro(null);
    try {
      await acao();
      await recarregar();
    } catch (e) {
      setErro(
        e instanceof Error ? e.message : "Nao foi possivel concluir a acao.",
      );
    }
  }

  if (carregando) {
    return <p className="text-sm text-slate-500">Carregando…</p>;
  }

  return (
    <div className="mx-auto max-w-5xl space-y-8">
      <div>
        <h1 className="text-2xl font-semibold text-unite-900">
          Estrutura organizacional
        </h1>
        <p className="mt-1 text-slate-500">
          Cadastre cargos, monte as equipes e acompanhe o organograma
          resultante.
        </p>
      </div>

      {erro && (
        <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </p>
      )}

      <SecaoCargos cargos={cargos} comAtualizacao={comAtualizacao} />
      <SecaoEquipes
        equipes={equipes}
        usuarios={usuarios}
        comAtualizacao={comAtualizacao}
      />
      {arvore && <SecaoArvore arvore={arvore} />}
    </div>
  );
}

// --------------------------------------------------------------- cargos

function SecaoCargos({
  cargos,
  comAtualizacao,
}: {
  cargos: Cargo[];
  comAtualizacao: (acao: () => Promise<unknown>) => Promise<void>;
}) {
  const [nome, setNome] = useState("");
  const [nivel, setNivel] = useState<Nivel>("Funcionario");
  const [enviando, setEnviando] = useState(false);

  async function criar(evento: FormEvent) {
    evento.preventDefault();
    setEnviando(true);
    await comAtualizacao(() => api.post("/api/cargos", { nome, nivel }));
    setNome("");
    setEnviando(false);
  }

  return (
    <section className="rounded-xl border border-unite-100 bg-white p-6">
      <h2 className="font-medium text-unite-900">Cargos</h2>

      <ul className="mt-3 divide-y divide-unite-100">
        {cargos.length === 0 && (
          <li className="py-2 text-sm text-slate-400">
            Nenhum cargo cadastrado ainda.
          </li>
        )}
        {cargos.map((c) => (
          <li
            key={c.id}
            className="flex items-center justify-between py-2 text-sm"
          >
            <span className="font-medium">{c.nome}</span>
            <span className="text-slate-500">
              {c.nivel} · {c.totalUsuarios} pessoa(s)
            </span>
          </li>
        ))}
      </ul>

      <form onSubmit={criar} className="mt-4 flex flex-wrap items-end gap-3">
        <label className="flex-1 min-w-40">
          <span className="mb-1 block text-sm font-medium text-slate-700">
            Nome do cargo
          </span>
          <input
            required
            maxLength={80}
            value={nome}
            onChange={(e) => setNome(e.target.value)}
            placeholder="Ex.: Analista de RH"
            className="w-full rounded-md border border-unite-100 px-3 py-2 outline-none focus:border-unite-400"
          />
        </label>

        <label>
          <span className="mb-1 block text-sm font-medium text-slate-700">
            Nivel
          </span>
          <select
            value={nivel}
            onChange={(e) => setNivel(e.target.value as Nivel)}
            className="rounded-md border border-unite-100 px-3 py-2 outline-none focus:border-unite-400"
          >
            {NIVEIS.map((n) => (
              <option key={n} value={n}>
                {n}
              </option>
            ))}
          </select>
        </label>

        <button
          type="submit"
          disabled={enviando}
          className="rounded-md bg-unite-700 px-4 py-2 text-sm font-medium text-white transition hover:bg-unite-900 disabled:opacity-60"
        >
          Adicionar
        </button>
      </form>
    </section>
  );
}

// -------------------------------------------------------------- equipes

function SecaoEquipes({
  equipes,
  usuarios,
  comAtualizacao,
}: {
  equipes: Equipe[];
  usuarios: MembroResumo[];
  comAtualizacao: (acao: () => Promise<unknown>) => Promise<void>;
}) {
  const [nome, setNome] = useState("");
  const [supervisorId, setSupervisorId] = useState("");
  const [enviando, setEnviando] = useState(false);

  async function criar(evento: FormEvent) {
    evento.preventDefault();
    setEnviando(true);
    await comAtualizacao(() =>
      api.post("/api/equipes", { nome, supervisorId: supervisorId || null }),
    );
    setNome("");
    setSupervisorId("");
    setEnviando(false);
  }

  return (
    <section className="rounded-xl border border-unite-100 bg-white p-6">
      <h2 className="font-medium text-unite-900">Equipes</h2>

      <div className="mt-3 space-y-4">
        {equipes.length === 0 && (
          <p className="text-sm text-slate-400">
            Nenhuma equipe cadastrada ainda.
          </p>
        )}

        {equipes.map((eq) => (
          <CartaoEquipe
            key={eq.id}
            equipe={eq}
            usuarios={usuarios}
            comAtualizacao={comAtualizacao}
          />
        ))}
      </div>

      <form
        onSubmit={criar}
        className="mt-5 flex flex-wrap items-end gap-3 border-t border-unite-100 pt-4"
      >
        <label className="flex-1 min-w-40">
          <span className="mb-1 block text-sm font-medium text-slate-700">
            Nome da equipe
          </span>
          <input
            required
            maxLength={80}
            value={nome}
            onChange={(e) => setNome(e.target.value)}
            placeholder="Ex.: Equipe Comercial"
            className="w-full rounded-md border border-unite-100 px-3 py-2 outline-none focus:border-unite-400"
          />
        </label>

        <label>
          <span className="mb-1 block text-sm font-medium text-slate-700">
            Supervisor
          </span>
          <select
            value={supervisorId}
            onChange={(e) => setSupervisorId(e.target.value)}
            className="rounded-md border border-unite-100 px-3 py-2 outline-none focus:border-unite-400"
          >
            <option value="">Sem supervisor por enquanto</option>
            {usuarios.map((u) => (
              <option key={u.id} value={u.id}>
                {u.nomeCompleto}
              </option>
            ))}
          </select>
        </label>

        <button
          type="submit"
          disabled={enviando}
          className="rounded-md bg-unite-700 px-4 py-2 text-sm font-medium text-white transition hover:bg-unite-900 disabled:opacity-60"
        >
          Criar equipe
        </button>
      </form>
    </section>
  );
}

function CartaoEquipe({
  equipe,
  usuarios,
  comAtualizacao,
}: {
  equipe: Equipe;
  usuarios: MembroResumo[];
  comAtualizacao: (acao: () => Promise<unknown>) => Promise<void>;
}) {
  const [novoMembroId, setNovoMembroId] = useState("");
  const idsNaEquipe = new Set(equipe.membros.map((m) => m.id));
  const candidatos = usuarios.filter((u) => !idsNaEquipe.has(u.id));

  async function adicionar(evento: FormEvent) {
    evento.preventDefault();
    if (!novoMembroId) return;
    await comAtualizacao(() =>
      api.post(`/api/equipes/${equipe.id}/membros`, {
        usuarioId: novoMembroId,
      }),
    );
    setNovoMembroId("");
  }

  return (
    <div className="rounded-lg border border-unite-100 p-4">
      <div className="flex items-center justify-between">
        <div>
          <p className="font-medium text-unite-900">{equipe.nome}</p>
          <p className="text-xs text-slate-500">
            Supervisor: {equipe.supervisor?.nomeCompleto ?? "nao definido"}
          </p>
        </div>
        <button
          type="button"
          onClick={() =>
            comAtualizacao(() => api.delete(`/api/equipes/${equipe.id}`))
          }
          className="text-xs text-red-600 hover:underline"
        >
          Excluir equipe
        </button>
      </div>

      <ul className="mt-3 flex flex-wrap gap-2">
        {equipe.membros.length === 0 && (
          <li className="text-xs text-slate-400">Sem membros ainda.</li>
        )}
        {equipe.membros.map((m) => (
          <li
            key={m.id}
            className="flex items-center gap-1.5 rounded-full bg-unite-50 px-2.5 py-1 text-xs text-unite-900"
          >
            {m.nomeCompleto}
            <button
              type="button"
              title="Remover da equipe"
              onClick={() =>
                comAtualizacao(() =>
                  api.delete(`/api/equipes/${equipe.id}/membros/${m.id}`),
                )
              }
              className="text-slate-400 hover:text-red-600"
            >
              ×
            </button>
          </li>
        ))}
      </ul>

      <form onSubmit={adicionar} className="mt-3 flex gap-2">
        <select
          value={novoMembroId}
          onChange={(e) => setNovoMembroId(e.target.value)}
          className="flex-1 rounded-md border border-unite-100 px-2 py-1.5 text-sm outline-none focus:border-unite-400"
        >
          <option value="">Adicionar membro…</option>
          {candidatos.map((u) => (
            <option key={u.id} value={u.id}>
              {u.nomeCompleto}
            </option>
          ))}
        </select>
        <button
          type="submit"
          disabled={!novoMembroId}
          className="rounded-md border border-unite-100 px-3 py-1.5 text-sm text-slate-600 transition hover:bg-unite-50 disabled:opacity-50"
        >
          Adicionar
        </button>
      </form>
    </div>
  );
}

// --------------------------------------------------------------- arvore

function SecaoArvore({ arvore }: { arvore: ArvoreOrganizacional }) {
  return (
    <section className="rounded-xl border border-unite-100 bg-white p-6">
      <h2 className="font-medium text-unite-900">Organograma</h2>
      <p className="mt-1 text-sm text-slate-500">
        Visao geral da hierarquia atual.
      </p>

      <div className="mt-4 space-y-4">
        {arvore.gerentes.length > 0 && (
          <div>
            <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-400">
              Gerencia
            </p>
            <div className="flex flex-wrap gap-2">
              {arvore.gerentes.map((g) => (
                <span
                  key={g.id}
                  className="rounded-full bg-unite-900 px-3 py-1 text-xs font-medium text-white"
                >
                  {g.nomeCompleto}
                </span>
              ))}
            </div>
          </div>
        )}

        {arvore.equipes.map((eq) => (
          <div key={eq.id} className="border-l-2 border-unite-100 pl-4">
            <p className="text-sm font-medium text-unite-900">
              {eq.nome}
              {eq.supervisor && (
                <span className="ml-2 font-normal text-slate-500">
                  · supervisor: {eq.supervisor.nomeCompleto}
                </span>
              )}
            </p>
            <div className="mt-1.5 flex flex-wrap gap-1.5">
              {eq.membros.length === 0 && (
                <span className="text-xs text-slate-400">—</span>
              )}
              {eq.membros.map((m) => (
                <span
                  key={m.id}
                  className="rounded-full bg-unite-50 px-2.5 py-0.5 text-xs text-unite-900"
                >
                  {m.nomeCompleto}
                </span>
              ))}
            </div>
          </div>
        ))}

        {arvore.semEquipe.length > 0 && (
          <div>
            <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-400">
              Sem equipe
            </p>
            <div className="flex flex-wrap gap-2">
              {arvore.semEquipe.map((u) => (
                <span
                  key={u.id}
                  className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs text-slate-600"
                >
                  {u.nomeCompleto}
                </span>
              ))}
            </div>
          </div>
        )}
      </div>
    </section>
  );
}
