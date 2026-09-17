import { useEffect, useRef, useState, type FormEvent } from "react";
import {
  api,
  urlArquivo,
  type Mensagem,
  type MembroResumo,
  type SalaResumo,
} from "../api/client";
import { useAuth } from "../auth/useAuth";
import { conectarChat } from "../lib/hubChat";
import { iniciais } from "../lib/iniciais";

export function Chat() {
  const { usuario } = useAuth();
  const [salas, setSalas] = useState<SalaResumo[]>([]);
  const [pessoas, setPessoas] = useState<MembroResumo[]>([]);
  const [salaAberta, setSalaAberta] = useState<SalaResumo | null>(null);
  const [mensagens, setMensagens] = useState<Mensagem[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  // O handler do hub vive fora do ciclo de render (a conexao e aberta uma vez
  // so), entao ele nao enxergaria o estado atual: a ref resolve isso.
  const salaAbertaRef = useRef<SalaResumo | null>(null);
  useEffect(() => {
    salaAbertaRef.current = salaAberta;
  }, [salaAberta]);

  useEffect(() => {
    Promise.all([
      api.get<SalaResumo[]>("/api/salas"),
      api.get<MembroResumo[]>("/api/usuarios"),
    ])
      .then(([s, p]) => {
        setSalas(s);
        setPessoas(p);
      })
      .catch((e) =>
        setErro(e instanceof Error ? e.message : "Nao foi possivel carregar as conversas."),
      )
      .finally(() => setCarregando(false));
  }, []);

  useEffect(() => {
    const conexao = conectarChat((mensagem) => {
      if (mensagem.salaId === salaAbertaRef.current?.id) {
        setMensagens((atuais) => [...atuais, mensagem]);
      }
      // O preview e a ordem da lista lateral valem para qualquer sala.
      setSalas((atuais) =>
        atuais.map((s) =>
          s.id === mensagem.salaId
            ? { ...s, ultimaMensagem: mensagem.texto, ultimaEm: mensagem.enviadaEm }
            : s,
        ),
      );
    });

    conexao
      .start()
      .catch(() => setErro("Sem conexao em tempo real. Recarregue a pagina."));

    return () => {
      conexao.stop();
    };
  }, []);

  async function abrir(sala: SalaResumo) {
    setErro(null);
    setSalaAberta(sala);
    setMensagens([]);
    try {
      setMensagens(await api.get<Mensagem[]>(`/api/salas/${sala.id}/mensagens`));
    } catch (e) {
      setErro(e instanceof Error ? e.message : "Nao foi possivel abrir a conversa.");
    }
  }

  async function conversarCom(pessoa: MembroResumo) {
    setErro(null);
    try {
      const sala = await api.post<SalaResumo>("/api/salas/privada", {
        usuarioId: pessoa.id,
      });
      // Conversa que ja existia nao pode entrar duas vezes na lista.
      setSalas((atuais) =>
        atuais.some((s) => s.id === sala.id) ? atuais : [sala, ...atuais],
      );
      await abrir(sala);
    } catch (e) {
      setErro(e instanceof Error ? e.message : "Nao foi possivel abrir a conversa.");
    }
  }

  async function enviar(texto: string) {
    if (!salaAberta) return;
    setEnviando(true);
    setErro(null);
    try {
      // A mensagem aparece pelo evento do hub, que o proprio autor recebe —
      // nada de inserir na lista aqui, senao ela apareceria duplicada.
      await api.post<Mensagem>(`/api/salas/${salaAberta.id}/mensagens`, { texto });
    } catch (e) {
      setErro(e instanceof Error ? e.message : "Nao foi possivel enviar.");
    } finally {
      setEnviando(false);
    }
  }

  if (carregando) {
    return <p className="text-sm text-slate-500">Carregando…</p>;
  }

  const semConversa = pessoas.filter(
    (p) => p.id !== usuario?.id && !salas.some((s) => s.outro.id === p.id),
  );

  return (
    <div className="flex h-full gap-4">
      <aside className="flex w-64 shrink-0 flex-col overflow-y-auto rounded-xl border border-unite-100 bg-white">
        <ListaLateral
          titulo="Conversas"
          vazio="Nenhuma conversa ainda."
          itens={salas.map((s) => ({
            chave: s.id,
            pessoa: s.outro,
            detalhe: s.ultimaMensagem,
            ativo: s.id === salaAberta?.id,
            aoClicar: () => abrir(s),
          }))}
        />
        <ListaLateral
          titulo="Pessoas"
          vazio="Todo mundo ja esta na sua lista."
          itens={semConversa.map((p) => ({
            chave: p.id,
            pessoa: p,
            detalhe: p.cargo,
            ativo: false,
            aoClicar: () => conversarCom(p),
          }))}
        />
      </aside>

      <section className="flex min-w-0 flex-1 flex-col rounded-xl border border-unite-100 bg-white">
        {erro && (
          <p className="m-3 rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</p>
        )}

        {salaAberta ? (
          <>
            <header className="border-b border-unite-100 px-5 py-3">
              <h1 className="font-medium text-unite-900">{salaAberta.outro.nomeCompleto}</h1>
              <p className="text-xs text-slate-500">{salaAberta.outro.cargo ?? "Sem cargo"}</p>
            </header>
            <Historico mensagens={mensagens} meuId={usuario?.id} />
            <CampoEnvio enviando={enviando} aoEnviar={enviar} />
          </>
        ) : (
          <p className="m-auto text-sm text-slate-500">
            Escolha uma pessoa ao lado para comecar a conversar.
          </p>
        )}
      </section>
    </div>
  );
}

// ------------------------------------------------------------ lista lateral

interface ItemLateral {
  chave: string;
  pessoa: MembroResumo;
  detalhe: string | null;
  ativo: boolean;
  aoClicar: () => void;
}

function ListaLateral({
  titulo,
  vazio,
  itens,
}: {
  titulo: string;
  vazio: string;
  itens: ItemLateral[];
}) {
  return (
    <div className="border-b border-unite-100 last:border-b-0">
      <h2 className="px-4 pt-4 pb-2 text-xs font-medium tracking-wide text-slate-400 uppercase">
        {titulo}
      </h2>
      {itens.length === 0 ? (
        <p className="px-4 pb-4 text-sm text-slate-500">{vazio}</p>
      ) : (
        <ul className="pb-2">
          {itens.map((item) => (
            <li key={item.chave}>
              <button
                type="button"
                onClick={item.aoClicar}
                className={`flex w-full items-center gap-3 px-4 py-2 text-left transition hover:bg-unite-50 ${
                  item.ativo ? "bg-unite-50" : ""
                }`}
              >
                <Avatar pessoa={item.pessoa} />
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm text-unite-900">
                    {item.pessoa.nomeCompleto}
                  </span>
                  {item.detalhe && (
                    <span className="block truncate text-xs text-slate-500">{item.detalhe}</span>
                  )}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function Avatar({ pessoa }: { pessoa: MembroResumo }) {
  return pessoa.fotoUrl ? (
    <img
      src={urlArquivo(pessoa.fotoUrl)}
      alt=""
      className="h-8 w-8 shrink-0 rounded-full object-cover"
    />
  ) : (
    <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-unite-100 text-xs font-medium text-unite-700">
      {iniciais(pessoa.nomeCompleto)}
    </span>
  );
}

// ---------------------------------------------------------------- historico

function Historico({ mensagens, meuId }: { mensagens: Mensagem[]; meuId?: string }) {
  const fim = useRef<HTMLDivElement>(null);

  // Conversa se le de baixo para cima: toda mensagem nova rola para o fim.
  useEffect(() => {
    fim.current?.scrollIntoView({ block: "end" });
  }, [mensagens]);

  return (
    <div className="flex-1 space-y-2 overflow-y-auto px-5 py-4">
      {mensagens.length === 0 && (
        <p className="text-sm text-slate-500">Nenhuma mensagem ainda. Diga um oi.</p>
      )}
      {mensagens.map((m) => {
        const minha = m.autorId === meuId;
        return (
          <div key={m.id} className={`flex ${minha ? "justify-end" : "justify-start"}`}>
            <div
              className={`max-w-[70%] rounded-2xl px-3 py-2 text-sm ${
                minha ? "bg-unite-700 text-white" : "bg-unite-50 text-unite-900"
              }`}
            >
              <p className="whitespace-pre-wrap break-words">{m.texto}</p>
              <p className={`mt-1 text-[11px] ${minha ? "text-unite-100" : "text-slate-500"}`}>
                {new Date(m.enviadaEm).toLocaleTimeString("pt-BR", {
                  hour: "2-digit",
                  minute: "2-digit",
                })}
              </p>
            </div>
          </div>
        );
      })}
      <div ref={fim} />
    </div>
  );
}

// -------------------------------------------------------------------- envio

function CampoEnvio({
  enviando,
  aoEnviar,
}: {
  enviando: boolean;
  aoEnviar: (texto: string) => Promise<void>;
}) {
  const [texto, setTexto] = useState("");

  async function submeter(e: FormEvent) {
    e.preventDefault();
    const limpo = texto.trim();
    if (!limpo || enviando) return;
    // Limpa antes da resposta: o campo travado ate o servidor responder
    // atrapalha quem digita rapido.
    setTexto("");
    await aoEnviar(limpo);
  }

  return (
    <form onSubmit={submeter} className="flex gap-2 border-t border-unite-100 p-3">
      <input
        value={texto}
        onChange={(e) => setTexto(e.target.value)}
        maxLength={4000}
        placeholder="Escreva uma mensagem"
        className="flex-1 rounded-md border border-unite-100 px-3 py-2 text-sm outline-none focus:border-unite-400"
      />
      <button
        type="submit"
        disabled={enviando}
        className="rounded-md bg-unite-700 px-4 py-2 text-sm font-medium text-white transition hover:bg-unite-900 disabled:opacity-50"
      >
        Enviar
      </button>
    </form>
  );
}
