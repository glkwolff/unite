import {
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type FormEvent,
} from "react";
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

const SEM_TEMPO_REAL = "Sem conexao em tempo real. Recarregue a pagina.";

// Tamanho da pagina do historico, usado na primeira carga e em cada rolagem
// para cima. Fica aqui, e nao solto nas chamadas, porque o mesmo numero
// responde "ainda tem mais?": veio a pagina cheia, pode ter mais.
const PAGINA = 50;

export function Chat() {
  const { usuario } = useAuth();
  const [salas, setSalas] = useState<SalaResumo[]>([]);
  const [pessoas, setPessoas] = useState<MembroResumo[]>([]);
  const [salaAberta, setSalaAberta] = useState<SalaResumo | null>(null);
  const [mensagens, setMensagens] = useState<Mensagem[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  // A conexao tem vida propria: cai e volta sozinha, sem ninguem clicar.
  // Por isso o aviso dela nao mora em `erro`, que abrir() e enviar() limpam.
  const [avisoConexao, setAvisoConexao] = useState<string | null>(null);

  const [temMais, setTemMais] = useState(false);
  const [carregandoAntigas, setCarregandoAntigas] = useState(false);
  // A trava de "ja estou buscando" e uma ref, nao um estado: o onScroll dispara
  // dezenas de vezes antes do proximo render, e nessas dezenas o estado ainda
  // estaria no valor velho — a mesma pagina seria pedida varias vezes.
  const buscandoAntigas = useRef(false);
  // Quem altera `mensagens` diz junto para onde o historico deve rolar. As duas
  // alteracoes querem coisas opostas: mensagem nova entra no fim e puxa a
  // leitura para baixo; pagina antiga entra no topo e deve deixar a leitura
  // parada. Deixar o Historico adivinhar isso comparando ids viraria charada.
  const [modoScroll, setModoScroll] = useState<"fim" | "manter">("fim");

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
    // O StrictMode monta o efeito duas vezes em dev: o cleanup do primeiro
    // monte para a conexao no meio da negociacao e o start() rejeita. Esse
    // start() fomos nos que abortamos, nao e falha de rede — a flag separa os
    // dois casos para o aviso nao nascer de um erro que provocamos.
    let cancelado = false;

    const conexao = conectarChat((mensagem) => {
      if (mensagem.salaId === salaAbertaRef.current?.id) {
        setModoScroll("fim");
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

    // withAutomaticReconnect so cobre queda depois de uma conexao estabelecida,
    // e nesse caso ele mesmo resolve: o aviso diz para esperar, nao para
    // recarregar. Recarregar a pagina e saida apenas quando a conexao desiste.
    conexao.onreconnecting(() => setAvisoConexao("Conexao caiu. Reconectando…"));
    conexao.onreconnected(() => setAvisoConexao(null));
    conexao.onclose(() => {
      if (!cancelado) setAvisoConexao(SEM_TEMPO_REAL);
    });

    conexao
      .start()
      .then(() => {
        if (!cancelado) setAvisoConexao(null);
      })
      .catch(() => {
        if (!cancelado) setAvisoConexao(SEM_TEMPO_REAL);
      });

    return () => {
      cancelado = true;
      conexao.stop();
    };
  }, []);

  async function abrir(sala: SalaResumo) {
    setErro(null);
    setSalaAberta(sala);
    setMensagens([]);
    setModoScroll("fim");
    setTemMais(false);
    try {
      const pagina = await api.get<Mensagem[]>(
        `/api/salas/${sala.id}/mensagens?limite=${PAGINA}`,
      );
      setMensagens(pagina);
      // Veio a pagina cheia: pode haver conversa mais antiga. Veio menos, e
      // porque chegamos ao comeco — e por isso a rota nao precisou de envelope.
      setTemMais(pagina.length === PAGINA);
    } catch (e) {
      setErro(e instanceof Error ? e.message : "Nao foi possivel abrir a conversa.");
    }
  }

  async function carregarAntigas() {
    if (buscandoAntigas.current || !temMais || !salaAberta || mensagens.length === 0) return;

    buscandoAntigas.current = true;
    setCarregandoAntigas(true);
    try {
      // O cursor vai como o texto EXATO que o servidor mandou em enviadaEm.
      // Passar por new Date(...).toISOString() acrescentaria o fuso e deslocaria
      // o cursor — o front pularia ou repetiria horas de conversa.
      const cursor = encodeURIComponent(mensagens[0].enviadaEm);
      const antigas = await api.get<Mensagem[]>(
        `/api/salas/${salaAberta.id}/mensagens?limite=${PAGINA}&antesDe=${cursor}`,
      );

      setTemMais(antigas.length === PAGINA);
      setModoScroll("manter");
      setMensagens((atuais) => {
        // Guarda contra o mesmo pedaco entrar duas vezes, se dois disparos de
        // rolagem se cruzarem. O id e o unico identificador confiavel aqui.
        const jaTenho = new Set(atuais.map((m) => m.id));
        return [...antigas.filter((m) => !jaTenho.has(m.id)), ...atuais];
      });
    } catch (e) {
      setErro(e instanceof Error ? e.message : "Nao foi possivel carregar o historico.");
    } finally {
      buscandoAntigas.current = false;
      setCarregandoAntigas(false);
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

  async function abrirCanalDaEquipe(canal: SalaResumo | null) {
    setErro(null);
    // O canal nasce no primeiro clique. Se ja esta na lista, abre direto; senao
    // o POST cria — ou devolve o que outro membro da equipe ja criou.
    if (canal) {
      await abrir(canal);
      return;
    }
    try {
      const sala = await api.post<SalaResumo>("/api/salas/equipe", {});
      setSalas((atuais) =>
        atuais.some((s) => s.id === sala.id) ? atuais : [sala, ...atuais],
      );
      await abrir(sala);
    } catch (e) {
      setErro(e instanceof Error ? e.message : "Nao foi possivel abrir o canal da equipe.");
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

  // "Conversas" continua sendo so privadas; o canal fica em secao propria, de
  // posicao fixa, em vez de misturado entre as conversas.
  const privadas = salas.filter((s) => s.tipo === "Privada");
  const canal = salas.find((s) => s.tipo === "Grupo") ?? null;

  const semConversa = pessoas.filter(
    (p) => p.id !== usuario?.id && !privadas.some((s) => s.outro?.id === p.id),
  );

  return (
    <div className="flex h-full gap-4">
      <aside className="flex w-64 shrink-0 flex-col overflow-y-auto rounded-xl border border-unite-100 bg-white">
        {usuario?.equipe && (
          <ListaLateral
            titulo="Minha equipe"
            vazio=""
            itens={[
              {
                chave: canal?.id ?? "canal-da-equipe",
                titulo: canal?.titulo ?? usuario.equipe,
                fotoUrl: null,
                detalhe: canal?.ultimaMensagem ?? "Abrir o canal da equipe",
                ativo: canal !== null && canal.id === salaAberta?.id,
                aoClicar: () => abrirCanalDaEquipe(canal),
              },
            ]}
          />
        )}
        <ListaLateral
          titulo="Conversas"
          vazio="Nenhuma conversa ainda."
          itens={privadas.map((s) => ({
            chave: s.id,
            titulo: s.titulo,
            fotoUrl: s.outro?.fotoUrl ?? null,
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
            titulo: p.nomeCompleto,
            fotoUrl: p.fotoUrl,
            detalhe: p.cargo,
            ativo: false,
            aoClicar: () => conversarCom(p),
          }))}
        />
      </aside>

      <section className="flex min-w-0 flex-1 flex-col rounded-xl border border-unite-100 bg-white">
        {avisoConexao && (
          <p className="m-3 rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-800">
            {avisoConexao}
          </p>
        )}

        {erro && (
          <p className="m-3 rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</p>
        )}

        {salaAberta ? (
          <>
            <header className="border-b border-unite-100 px-5 py-3">
              <h1 className="font-medium text-unite-900">{salaAberta.titulo}</h1>
              <p className="text-xs text-slate-500">
                {salaAberta.tipo === "Grupo"
                  ? "Canal da equipe"
                  : (salaAberta.outro?.cargo ?? "Sem cargo")}
              </p>
            </header>
            <Historico
              mensagens={mensagens}
              meuId={usuario?.id}
              modoScroll={modoScroll}
              temMais={temMais}
              carregandoAntigas={carregandoAntigas}
              // Em grupo o balao sozinho nao diz quem falou.
              mostrarAutor={salaAberta.tipo === "Grupo"}
              aoChegarNoTopo={carregarAntigas}
            />
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
  titulo: string;
  // O canal nao tem foto: cai nas iniciais do nome da equipe.
  fotoUrl: string | null;
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
                <Avatar nome={item.titulo} fotoUrl={item.fotoUrl} />
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm text-unite-900">{item.titulo}</span>
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

// Recebe nome e foto soltos, e nao uma MembroResumo, porque o canal de equipe
// nao tem pessoa nenhuma: ali a inicial sai do nome da equipe.
function Avatar({ nome, fotoUrl }: { nome: string; fotoUrl: string | null }) {
  return fotoUrl ? (
    <img
      src={urlArquivo(fotoUrl)}
      alt=""
      className="h-8 w-8 shrink-0 rounded-full object-cover"
    />
  ) : (
    <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-unite-100 text-xs font-medium text-unite-700">
      {iniciais(nome)}
    </span>
  );
}

// ---------------------------------------------------------------- historico

function Historico({
  mensagens,
  meuId,
  modoScroll,
  temMais,
  carregandoAntigas,
  mostrarAutor,
  aoChegarNoTopo,
}: {
  mensagens: Mensagem[];
  meuId?: string;
  modoScroll: "fim" | "manter";
  temMais: boolean;
  carregandoAntigas: boolean;
  mostrarAutor: boolean;
  aoChegarNoTopo: () => void;
}) {
  const area = useRef<HTMLDivElement>(null);
  const alturaAnterior = useRef(0);

  // useLayoutEffect, e nao useEffect: o ajuste tem que acontecer depois de o DOM
  // crescer e ANTES de o navegador pintar. Com useEffect a pessoa ve o salto e
  // so depois a correcao.
  useLayoutEffect(() => {
    const el = area.current;
    if (!el) return;

    if (modoScroll === "manter") {
      // O conteudo cresceu para cima: devolver exatamente o tanto que cresceu
      // deixa a mensagem que estava sendo lida parada na mesma altura da tela.
      el.scrollTop = el.scrollHeight - alturaAnterior.current;
    } else {
      el.scrollTop = el.scrollHeight;
    }

    alturaAnterior.current = el.scrollHeight;
  }, [mensagens, modoScroll]);

  function aoRolar() {
    // Folga de 60px porque o navegador raramente entrega scrollTop exatamente
    // zero: com "=== 0" a paginacao quase nunca disparava.
    if (area.current && area.current.scrollTop < 60) aoChegarNoTopo();
  }

  return (
    <div
      ref={area}
      onScroll={aoRolar}
      className="flex-1 space-y-2 overflow-y-auto px-5 py-4"
    >
      {carregandoAntigas && (
        <p className="text-center text-xs text-slate-400">Carregando historico…</p>
      )}
      {!temMais && mensagens.length > 0 && (
        <p className="text-center text-xs text-slate-400">Comeco da conversa.</p>
      )}
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
              {mostrarAutor && !minha && (
                <p className="text-[11px] font-medium text-unite-700">{m.autorNome}</p>
              )}
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
