# Unite

Plataforma de colaboração e gestão empresarial: comunicação interna centralizada em
**feed de notícias** (avisos institucionais com confirmação de ciência) e **chat em
tempo real** (privado e em grupo).

Trabalho da disciplina de Planejamento de Projeto de Sistema Visual — defesa em 19/11/2026.

**Equipe:** Deyvid Rocha · Gabriel Wolff · Isadora Ribeiro · Kamila Ferreira

---

## Stack

| Camada | Tecnologia |
| --- | --- |
| Backend | .NET 8 (LTS) · ASP.NET Core Web API · SignalR |
| Dados | SQLite em modo WAL · Entity Framework Core 8 |
| Autenticação | ASP.NET Core Identity · JWT · 2FA com Google Authenticator (TOTP) |
| Frontend | React 19 · TypeScript · Vite · Tailwind CSS 4 |
| Testes | xUnit · WebApplicationFactory *(a partir de 05/11)* |

.NET 8 e não o 10 previsto no escopo: é a versão instalada nos computadores da faculdade.

Roda em **instância única**: sem Redis, sem RabbitMQ, sem Docker. O acesso a dados está
atrás do EF Core, então trocar o SQLite por PostgreSQL é mudar o provider.

---

## Pré-requisitos

- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0) (LTS)
- [Node.js 20+](https://nodejs.org)
- Ferramenta de migrations:
  ```bash
  dotnet tool install --global dotnet-ef
  ```
  Se `dotnet ef` não for encontrado depois, adicione ao PATH:
  ```bash
  export PATH="$PATH:$HOME/.dotnet/tools"
  ```

---

## Como rodar

### 1. Backend

```bash
cd ChatApi

# Obrigatório: a chave de assinatura do JWT não vai para o repositório.
dotnet user-secrets set "Jwt:Chave" "$(openssl rand -base64 48)"

dotnet run
```

A API sobe em **http://localhost:5000**. As migrations são aplicadas automaticamente
no startup e o banco `chat.db` é criado na primeira execução.

> Sem `Jwt:Chave` a aplicação falha no startup com uma mensagem explicando o comando.
> Isso é intencional: o repositório é público e nenhum segredo pode ser versionado.

### 2. Frontend

```bash
cd chat-web
npm install
npm run dev
```

A interface sobe em **http://localhost:5173**. Se a API estiver em outra porta, copie
`.env.example` para `.env` e ajuste `VITE_API_URL`.

---

## Estrutura

```
ChatApi/
├── Controllers/AuthController.cs      cadastro, login (com 2FA), /eu e 2FA do perfil
├── Controllers/PerfilController.cs    editar nome, enviar/remover foto
├── Controllers/CargosController.cs    CRUD de cargos
├── Controllers/EquipesController.cs   CRUD de equipes, membros e /arvore
├── Controllers/UsuariosController.cs  listagem de pessoas e atribuição de cargo
├── Controllers/SalasController.cs     conversas privadas e mensagens
├── Controllers/PostagensController.cs feed de notícias: listar, publicar, remover
├── Hubs/ChatHub.cs                    canal de tempo real (só recebe)
├── Data/AppDbContext.cs               chaves, índices, delete e datas em UTC
├── Data/AppDbContextFactory.cs        usado só pelo dotnet ef
├── Data/SeedCargos.cs                 cria os 4 cargos padrão numa base nova
├── Models/                            Usuario, Cargo, Equipe, Sala,
│                                       SalaUsuario, Mensagem, Postagem, Ciencia
├── Dtos/                              entrada (com validação) e saída (XxxDto.De)
├── Services/TokenService.cs           emissão do JWT
├── Services/Permissoes.cs             quem pode o quê (única fonte da regra)
├── Services/ProvedorIdUsuario.cs      claim "sub" como id do usuario no SignalR
├── wwwroot/uploads/perfis/            fotos de perfil (fora do git)
└── Migrations/

chat-web/src/
├── api/client.ts                   wrapper de fetch com Bearer, PUT/DELETE e upload
├── auth/                           AuthContext, useAuth, RotaProtegida
├── components/Layout.tsx           casca com sidebar e topbar
├── components/CodigoAuthenticator  QR code e campo do código do 2FA
├── components/SegundaEtapaLogin    tela do código depois da senha
├── lib/permissoes.ts               espelho das regras do backend, só para a UI
├── lib/hubChat.ts                  conexão SignalR de leitura do chat
└── pages/                          Login, Cadastro, Feed, Perfil, Equipes,
                                    Pessoas, Chat
```

---

## Migrations

```bash
cd ChatApi
dotnet ef migrations add NomeDaMigration
dotnet ef database update
```

O `AppDbContextFactory` existe para que o `dotnet ef` não precise subir o host da
aplicação — assim as migrations funcionam sem a chave JWT configurada.

---

## Endpoints

| Método | Rota | Permissão | Descrição |
| --- | --- | --- | --- |
| POST | `/api/auth/registrar` | — | Cria a conta (depois é só entrar) |
| POST | `/api/auth/login` | — | Senha e, se precisar, o código do 2FA; devolve o token |
| GET | `/api/auth/eu` | autenticado | Dados do usuário logado |
| POST | `/api/auth/2fa/configurar` | autenticado | Gera a chave e o QR code do Authenticator |
| POST | `/api/auth/2fa/ativar` | autenticado | Confere o 1º código e liga o 2FA |
| POST | `/api/auth/2fa/desativar` | autenticado, nível que não exige | Desliga o 2FA (pede um código válido) |
| PUT | `/api/perfil` | autenticado | Atualiza o nome completo |
| POST | `/api/perfil/foto` | autenticado | Upload da foto (multipart, campo `arquivo`) |
| DELETE | `/api/perfil/foto` | autenticado | Remove a foto atual |
| GET | `/api/usuarios` | autenticado | Lista de pessoas (sem e-mail) |
| PUT | `/api/usuarios/{id}/cargo` | gerente¹ | Atribui ou remove o cargo de alguém |
| GET | `/api/cargos` | autenticado | Lista os cargos |
| POST | `/api/cargos` | diretor | Cria um cargo |
| PUT/DELETE | `/api/cargos/{id}` | diretor | Atualiza/remove um cargo |
| GET | `/api/equipes` | autenticado | Lista as equipes |
| GET | `/api/equipes/arvore` | autenticado | Organograma completo |
| POST | `/api/equipes` | gerente | Cria uma equipe |
| PUT/DELETE | `/api/equipes/{id}` | gerente | Atualiza/remove uma equipe |
| POST | `/api/equipes/{id}/membros` | gerente² | Adiciona um membro à equipe |
| DELETE | `/api/equipes/{id}/membros/{usuarioId}` | gerente² | Remove um membro da equipe |
| GET | `/api/salas` | autenticado | Minhas conversas, a mais recente primeiro |
| POST | `/api/salas/privada` | autenticado | Abre (ou reabre) a conversa com alguém |
| POST | `/api/salas/equipe` | autenticado⁴ | Abre (ou cria) o canal da minha equipe |
| GET | `/api/salas/{id}/mensagens` | participante | Histórico, `?limite=50&antesDe=<data>` |
| POST | `/api/salas/{id}/mensagens` | participante | Envia a mensagem e avisa o hub |
| WS | `/chat` | autenticado³ | Hub SignalR: emite `MensagemRecebida` |
| GET | `/api/postagens?antes=&limite=` | autenticado | Feed, do mais recente ao mais antigo⁵ |
| POST | `/api/postagens` | supervisor⁶ | Publica um aviso |
| DELETE | `/api/postagens/{id}` | autor ou gerente | Remove um aviso |
| POST | `/api/postagens/{id}/ciente` | autenticado, menos o autor | Marca "Ciente" (sem desfazer) |
| GET | `/api/postagens/{id}/ciencias` | ver tabela abaixo | Quem marcou "Ciente" e quando |

"Gerente" na coluna significa **gerente ou acima** — a hierarquia é cumulativa.
¹ O gerente só distribui cargos de Supervisor para baixo, e não altera quem já é
diretor ou gerente. ² Ou o supervisor daquela equipe específica. ³ Pelo WebSocket o
token vai na query string (`?access_token=`), porque o protocolo não manda header.
⁴ Exige estar em alguma equipe: sem equipe a rota devolve 400.
⁵ Paginação por cursor: `antes` é o `publicadaEm` da última postagem recebida; a resposta
traz `{ itens, temMais }`. `limite` vai de 1 a 50 (padrão 20). Datas saem em UTC (`...Z`)
e o front exibe no fuso de quem lê. ⁶ Aviso com `institucional: true` exige gerente.

## Permissões

```
                 cargos   equipes    membros           atribuir cargo
Diretor           CRUD     CRUD      qualquer equipe   qualquer nível
Gerente           ler      CRUD      qualquer equipe   até Supervisor
Supervisor        ler      ler       só a sua equipe   não
Funcionário       ler      ler       não               não
```

Feed de notícias:

```
                 publicar   aviso institucional   remover postagem
Diretor           sim        sim                   qualquer uma
Gerente           sim        sim                   qualquer uma
Supervisor        sim        não                   só as próprias
Funcionário       não        não                   —
```

Botão "Ciente": qualquer pessoa marca em qualquer postagem que não seja a própria.
Ver **quem** marcou depende do nível:

```
                 em quais postagens                    quais leitores aparecem
Diretor           todas                                 todos
Gerente           de supervisores e funcionários        todos
                  + as próprias
Supervisor        só as próprias                        só funcionários
Funcionário       nenhuma                               —
```

Quem não tem cargo conta como funcionário. Cada postagem já chega do servidor com
`podeMarcarCiente`, `podeVerCiencias`, `totalCiencias` e `cienteEm` calculados, então o
front não replica essas regras.

Ler é liberado para qualquer autenticado: o organograma é público dentro da empresa.
O que a hierarquia restringe é escrever.

A regra mora inteira em `ChatApi/Services/Permissoes.cs`, e cada action de controller
faz uma pergunta a ela (`if (!await permissoes.PodeGerenciarCargosAsync()) return Forbid();`).
O nível é lido **do banco**, não do claim `nivel` do JWT — assim uma promoção ou um
rebaixamento valem na requisição seguinte, e não só no próximo login. O front tem um
espelho dessas regras em `chat-web/src/lib/permissoes.ts`, usado apenas para não
desenhar botão que o servidor vai recusar.

**Quatro níveis, não três:** o escopo previa Gerente, Supervisor e Funcionário. A equipe
acrescentou o **Diretor** acima do Gerente para ter um nível com acesso total — é o único que
mexe nos cargos e atribui qualquer nível.

**Primeiro acesso:** numa base vazia não existe diretor, e sem diretor ninguém atribui
cargo. Por isso o startup cria os quatro cargos padrão e a primeira conta registrada
assume a diretoria. Pelo mesmo motivo o sistema recusa tirar o cargo do último diretor
e recusa apagar um cargo que ainda tem pessoas — apagar o cargo de diretor tiraria a
diretoria inteira de uma vez, e a semente do startup não volta a rodar com a tabela
de cargos já preenchida.

Fotos de perfil ficam em `ChatApi/wwwroot/uploads/perfis/` (fora do controle de versão) e
são servidas como arquivo estático em `/uploads/perfis/<arquivo>`.

Em desenvolvimento o Swagger UI fica em `/swagger` e o contrato OpenAPI em `/swagger/v1/swagger.json`.

---

## Chat privado e canal de equipe

A escrita é HTTP, a leitura é SignalR: o front envia por `POST /api/salas/{id}/mensagens`
e o `SalasController` grava e dispara o broadcast via `IHubContext<ChatHub>`. O hub não
tem método nenhum — validação e autorização ficam num caminho só, o do controller.

O broadcast usa `Clients.Users(...)`, que endereça pelo id do usuário e dispensa
gerenciar grupos a cada conexão. Para isso o `ProvedorIdUsuario` aponta a claim `sub`:
o provedor padrão do SignalR procura `ClaimTypes.NameIdentifier`, que não existe aqui
porque o JWT é lido com `MapInboundClaims = false`.

A sala privada nasce sob demanda e é idempotente: `POST /api/salas/privada` devolve a
conversa que já existia entre as duas pessoas ou cria uma nova. A sala não tem nome: o
título exibido é o nome do outro participante (ou da equipe, no canal), montado a cada
resposta.

Conversa não segue hierarquia: o `SalasController` não pergunta nada à `Permissoes`. A
única trava é ser participante da sala — quem não é leva 403.

### O canal da equipe

Grupo, aqui, é sempre o **canal automático de uma equipe** — não existe grupo com
participantes escolhidos à mão. O canal nasce no primeiro clique
(`POST /api/salas/equipe`, idempotente) e não junto com a equipe, senão as equipes que já
estão no banco nunca ganhariam canal.

**O canal não guarda a lista de participantes.** Participante dele é quem tem
`Usuario.EquipeId` igual ao `Sala.EquipeId`, consultado na hora; o canal não tem uma
linha em `SalaUsuarios`. Gravar essa lista criaria duas fontes da mesma verdade, que
divergiriam no instante em que alguém entrasse ou saísse da equipe. Por isso `AdicionarMembro` e `RemoverMembro` não têm uma linha sobre chat, e
quem entra na equipe passa a receber a próxima mensagem sem religar a conexão.

Os dois caminhos de participação ficam lado a lado em `SalasController.ParticipantesAsync`,
que alimenta tanto a autorização quanto a lista de destinatários do broadcast — o envio
nunca mira gente diferente da que a autorização considerou.

Três consequências aceitas, todas visíveis na leitura do código:

- quem **sai da equipe perde o acesso** ao histórico do canal (as mensagens dele
  continuam lá, com o nome, para quem ficou);
- **diretor e gerente não participam de canal nenhum**, porque não têm equipe — coerente
  com "conversa não segue hierarquia";
- o **supervisor precisa ser membro da equipe**, e não só o `SupervisorId` dela: a
  participação é uma regra só, `EquipeId`, e não duas.

Apagar a equipe apaga o canal e as mensagens junto (`Cascade` no `AppDbContext`).

### Histórico paginado

`GET /api/salas/{id}/mensagens` aceita `?antesDe=<data>` e devolve o pedaço
imediatamente anterior ao cursor, apoiado no índice `(SalaId, EnviadaEm)`. A resposta é
`{ itens, temMais }`, igual à do feed: o servidor busca um item a mais que o limite só para
saber se existe página anterior.

No front, quem altera a lista de mensagens declara para onde o histórico deve rolar
(`modoScroll`): mensagem nova entra no fim e puxa a leitura para baixo; página antiga
entra no topo e a rolagem é devolvida na exata altura que o conteúdo cresceu, para a
mensagem que estava sendo lida não sair do lugar. O ajuste é um `useLayoutEffect`, antes
da pintura, e a trava de "já estou buscando" é uma `ref`, porque o `onScroll` dispara
dezenas de vezes antes do próximo render.

---

## Verificação em duas etapas

Código de 6 dígitos do **Google Authenticator** (TOTP), pelo provedor que já vem no
ASP.NET Core Identity. Não usa nenhum serviço do Google: a chave fica no banco e no celular.

**Por que não Login com Google?** O documento de escopo previa OAuth com o Google, mas a
equipe optou pelo TOTP: o acesso fica inteiramente empresarial, sem vínculo com a conta
Google pessoal de ninguém, e funciona em empresas que não usam Google Workspace — o
público-alvo são empresas pequenas e médias. Qualquer app autenticador (Google, Microsoft,
Authy) serve.

- **Gerente e Diretor:** obrigatório. No primeiro login após a senha, a tela já mostra o
  QR code para cadastrar. A regra está em `Permissoes.ExigeDoisFatores`.
- **Supervisor e Funcionário:** opcional, ativado em *Meu perfil*.
- **Um endpoint só:** `POST /api/auth/login` recebe `{ email, senha, codigo? }`. Se a pessoa
  precisa de 2FA e o código não veio, a resposta é `{ etapa: "codigo" }` ou
  `{ etapa: "configurar", chave, uri }` (QR code); o front pede o código e manda o login de
  novo, agora com ele.
- **Tentativas:** 5 códigos errados seguidos travam a conta por 5 minutos (lockout do Identity).

## Progresso

| Data | Etapa | Situação |
| --- | --- | --- |
| 27/08 | Setup do projeto | ✅ |
| 03/09 | Autenticação base | ✅ |
| 10/09 | Perfil e estrutura organizacional | ✅ |
| 17/09 | Permissões e hierarquia | ✅ |
| 01/10 | Chat privado | ✅ |
| 08/10 | Chat em grupo e histórico | ✅ |
| 15/10 | Feed de notícias | ✅ |
| 22/10 | Botão "Ciente" | ✅ (adiantado) |
| 29/10 | 2FA com Google Authenticator | ✅ (adiantado) |
| 05/11 | Testes de integração | — |
| 12/11 | Ajustes finais | — |
| 19/11 | Defesa do código | — |
