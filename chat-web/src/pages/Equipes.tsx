import { useEffect, useState, type FormEvent } from "react";
import {
  api,
  type ArvoreOrganizacional,
  type Cargo,
  type Equipe,
  type MembroResumo,
  type Nivel,
} from "../api/client";
import { useAuth } from "../auth/useAuth";
import { podeAdministrar, podeGerenciarMembros } from "../auth/permissoes";

const NIVEIS: Nivel[] = ["Diretor", "Gerente", "Supervisor", "Funcionario"];

export function Equipes() {
  const { usuario } = useAuth();
  const [cargos, setCargos] = useState<Cargo[]>([]);
  const [equipes, setEquipes] = useState<Equipe[]>([]);
  const [usuarios, setUsuarios] = useState<MembroResumo[]>([]);
  const [arvore, setArvore] = useState<ArvoreOrganizacional | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  const admin = podeAdministrar(usuario?.nivel);

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
          {admin
            ? "Cadastre cargos, monte as equipes e acompanhe o organograma resultante."
            : "Voce esta vendo a estrutura em modo leitura: alterar cargos e equipes e da gerencia para cima."}
        </p>
      </div>

      {erro && (
        <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">
          {erro}
        </p>
      )}

      <SecaoCargos cargos={cargos} admin={admin} comAtualizacao={comAtualizacao} />
      {admin && (
        <SecaoPessoas
          usuarios={usuarios}
          cargos={cargos}
          equipes={equipes}
          meuId={usuario?.id}
          comAtualizacao={comAtualizacao}
        />
      )}
      <SecaoEquipes
        equipes={equipes}
        usuarios={usuarios}
        admin={admin}
        meuId={usuario?.id}
        meuNivel={usuario?.nivel ?? null}
        comAtualizacao={comAtualizacao}
      />
      {arvore && <SecaoArvore arvore={arvore} />}
    </div>
  );
}

// --------------------------------------------------------------- cargos

function SecaoCargos({
  cargos,
  admin,
  comAtualizacao,
}: {
  cargos: Cargo[];
  admin: boolean;
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

      {admin && (
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
      )}
    </section>
  );
}

// --------------------------------------------------------------- pessoas

/** Onde cargo e equipe sao atribuidos. So a gerencia chega aqui. */
function SecaoPessoas({
  usuarios,
  cargos,
  equipes,
  meuId,
  comAtualizacao,
}: {
  usuarios: MembroResumo[];
  cargos: Cargo[];
  equipes: Equipe[];
  meuId: string | undefined;
  comAtualizacao: (acao: () => Promise<unknown>) => Promise<void>;
}) {
  return (
    <section className="rounded-xl border border-unite-100 bg-white p-6">
      <h2 className="font-medium text-unite-900">Pessoas</h2>
      <p className="mt-1 text-sm text-slate-500">
        Defina o cargo — que e o que concede permissao — e a equipe de cada um.
      </p>

      <ul className="mt-4 divide-y divide-unite-100">
        {usuarios.map((u) => (
          <li
            key={u.id}
            className="flex flex-wrap items-center justify-between gap-3 py-2.5"
          >
            <span className="text-sm font-medium text-unite-900">
              {u.nomeCompleto}
              {u.id === meuId && (
                <span className="ml-2 text-xs font-normal text-slate-400">
                  (voce)
                </span>
              )}
            </span>

            <div className="flex flex-wrap items-center gap-2">
              <select
                // O proprio cargo fica travado: promover a si mesmo furaria a
                // hierarquia inteira (a API recusa isso de qualquer forma).
                disabled={u.id === meuId}
                value={u.cargoId ?? ""}
                onChange={(e) =>
                  comAtualizacao(() =>
                    api.put(`/api/usuarios/${u.id}/cargo`, {
                      cargoId: e.target.value || null,
                    }),
                  )
                }
                className="rounded-md border border-unite-100 px-2 py-1.5 text-sm outline-none focus:border-unite-400 disabled:bg-slate-50 disabled:text-slate-400"
              >
                <option value="">Sem cargo</option>
                {cargos.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.nome} · {c.nivel}
                  </option>
                ))}
              </select>

              <select
                value={u.equipeId ?? ""}
                onChange={(e) =>
                  comAtualizacao(() =>
                    api.put(`/api/usuarios/${u.id}/equipe`, {
                      equipeId: e.target.value || null,
                    }),
                  )
                }
                className="rounded-md border border-unite-100 px-2 py-1.5 text-sm outline-none focus:border-unite-400"
              >
                <option value="">Sem equipe</option>
                {equipes.map((eq) => (
                  <option key={eq.id} value={eq.id}>
                    {eq.nome}
                  </option>
                ))}
              </select>
            </div>
          </li>
        ))}
      </ul>
    </section>
  );
}

// -------------------------------------------------------------- equipes

function SecaoEquipes({
  equipes,
  usuarios,
  admin,
  meuId,
  meuNivel,
  comAtualizacao,
}: {
  equipes: Equipe[];
  usuarios: MembroResumo[];
  admin: boolean;
  meuId: string | undefined;
  meuNivel: Nivel | null;
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
            admin={admin}
            podeMexerNosMembros={podeGerenciarMembros(
              meuNivel,
              meuId,
              eq.supervisor?.id ?? null,
            )}
            comAtualizacao={comAtualizacao}
          />
        ))}
      </div>

      {admin && (
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
      )}
    </section>
  );
}

function CartaoEquipe({
  equipe,
  usuarios,
  admin,
  podeMexerNosMembros,
  comAtualizacao,
}: {
  equipe: Equipe;
  usuarios: MembroResumo[];
  admin: boolean;
  podeMexerNosMembros: boolean;
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
        {admin && (
          <button
            type="button"
            onClick={() =>
              comAtualizacao(() => api.delete(`/api/equipes/${equipe.id}`))
            }
            className="text-xs text-red-600 hover:underline"
          >
            Excluir equipe
          </button>
        )}
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
            {podeMexerNosMembros && (
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
            )}
          </li>
        ))}
      </ul>

      {podeMexerNosMembros && (
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
      )}
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
