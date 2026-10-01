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
| Dados | SQLite em modo WAL · Entity Framework Core 10 |
| Autenticação | ASP.NET Core Identity · JWT · Google OAuth *(a partir de 29/10)* |
| Frontend | React 19 · TypeScript · Vite · Tailwind CSS 4 |
| Testes | xUnit · WebApplicationFactory *(a partir de 05/11)* |

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
├── Controllers/AuthController.cs      cadastro, login e /eu
├── Controllers/PerfilController.cs    ver/editar perfil, upload de foto
├── Controllers/CargosController.cs    CRUD de cargos
├── Controllers/EquipesController.cs   CRUD de equipes, membros e /arvore
├── Controllers/UsuariosController.cs  listagem de pessoas e atribuição de cargo
├── Controllers/SalasController.cs     conversas privadas e mensagens
├── Controllers/PostagensController.cs feed de notícias: listar, publicar, remover
├── Hubs/ChatHub.cs                    canal de tempo real (só recebe)
├── Data/AppDbContext.cs               mapeamento das entidades
├── Data/AppDbContextFactory.cs        usado só pelo dotnet ef
├── Data/SeedCargos.cs                 cria os 4 cargos padrão numa base nova
├── Models/                            Usuario, Cargo, Equipe, Sala,
│                                       SalaUsuario, Mensagem, Postagem, Ciencia
├── Services/TokenService.cs           emissão do JWT
├── Services/Permissoes.cs             quem pode o quê (RF11)
├── Services/MapeamentoOrganizacao.cs  Usuario -> MembroResumoDto
├── Services/ProvedorIdUsuario.cs      claim "sub" como id do usuario no SignalR
├── wwwroot/uploads/perfis/            fotos de perfil (fora do git)
└── Migrations/

chat-web/src/
├── api/client.ts                   wrapper de fetch com Bearer, PUT/DELETE e upload
├── auth/                           AuthContext, useAuth, RotaProtegida
├── components/Layout.tsx           casca com sidebar e topbar
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
| POST | `/api/auth/registrar` | — | Cria a conta e já devolve o token |
| POST | `/api/auth/login` | — | Autentica e devolve o token |
| GET | `/api/auth/eu` | autenticado | Dados do usuário logado |
| GET | `/api/perfil` | autenticado | Dados do próprio perfil |
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
| GET | `/api/salas/{id}/mensagens` | participante | Histórico da conversa, `?limite=50` |
| POST | `/api/salas/{id}/mensagens` | participante | Envia a mensagem e avisa o hub |
| WS | `/chat` | autenticado³ | Hub SignalR: emite `MensagemRecebida` |
| GET | `/api/postagens?antes=&limite=` | autenticado | Feed, do mais recente ao mais antigo⁴ |
| GET | `/api/postagens/{id}` | autenticado | Uma postagem |
| POST | `/api/postagens` | supervisor⁵ | Publica um aviso |
| DELETE | `/api/postagens/{id}` | autor ou gerente | Remove um aviso |
| POST | `/api/postagens/{id}/ciente` | autenticado, menos o autor | Marca "Ciente" (sem desfazer) |
| GET | `/api/postagens/{id}/ciencias` | ver tabela abaixo | Quem marcou "Ciente" e quando |

"Gerente" na coluna significa **gerente ou acima** — a hierarquia é cumulativa.
¹ O gerente só distribui cargos de Supervisor para baixo, e não altera quem já é
diretor ou gerente. ² Ou o supervisor daquela equipe específica. ³ Pelo WebSocket o
token vai na query string (`?access_token=`), porque o protocolo não manda header.
⁴ Paginação por cursor: `antes` é o `publicadaEm` da última postagem recebida; a resposta
traz `{ itens, temMais }`. `limite` vai de 1 a 50 (padrão 20). Datas saem em UTC (`...Z`)
e o front exibe no fuso de quem lê. ⁵ Aviso com `institucional: true` exige gerente.

## Permissões (RF11)

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

## Chat privado

A escrita é HTTP, a leitura é SignalR: o front envia por `POST /api/salas/{id}/mensagens`
e o `SalasController` grava e dispara o broadcast via `IHubContext<ChatHub>`. O hub não
tem método nenhum — validação e autorização ficam num caminho só, o do controller.

O broadcast usa `Clients.Users(...)`, que endereça pelo id do usuário e dispensa
gerenciar grupos a cada conexão. Para isso o `ProvedorIdUsuario` aponta a claim `sub`:
o provedor padrão do SignalR procura `ClaimTypes.NameIdentifier`, que não existe aqui
porque o JWT é lido com `MapInboundClaims = false`.

A sala privada nasce sob demanda e é idempotente: `POST /api/salas/privada` devolve a
conversa que já existia entre as duas pessoas ou cria uma nova. O `Sala.Nome` fica vazio
na conversa privada — o título exibido é o nome do outro participante, montado a cada
resposta, para não envelhecer quando alguém editar o perfil.

Conversa não segue hierarquia: `SalasController` não tem `[ExigeNivel]`. A única trava é
ser participante da sala — quem não é leva 403.

---

## Progresso

| Data | Etapa | Situação |
| --- | --- | --- |
| 27/08 | Setup do projeto | ✅ |
| 03/09 | Autenticação base | ✅ |
| 10/09 | Perfil e estrutura organizacional | ✅ |
| 17/09 | Permissões e hierarquia | ✅ |
| 01/10 | Chat privado | ✅ |
| 08/10 | Chat em grupo e histórico | — |
| 15/10 | Feed de notícias | ✅ (adiantado) |
| 22/10 | Botão "Ciente" | ✅ (adiantado) |
| 29/10 | Login com Google | — |
| 05/11 | Testes de integração | — |
| 12/11 | Ajustes finais | — |
| 19/11 | Defesa do código | — |
