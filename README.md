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
| Backend | .NET 8 (LTS) · ASP.NET Core Web API · SignalR *(a partir de 01/10)* |
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
├── Controllers/UsuariosController.cs  listagem resumida e lotacao (cargo/equipe)
├── Autorizacao/ExigeNivelAttribute.cs filtro [ExigeNivel] nas rotas restritas
├── Autorizacao/Permissoes.cs          as regras de quem pode o que
├── Autorizacao/UsuarioLogado.cs       id pelo token, nivel pelo banco
├── Data/CargosPadrao.cs               semente dos quatro cargos iniciais
├── Data/AppDbContext.cs               mapeamento das entidades
├── Data/AppDbContextFactory.cs        usado só pelo dotnet ef
├── Models/                            Usuario, Cargo, Equipe, Sala,
│                                       SalaUsuario, Mensagem, Postagem, Ciencia
├── Services/TokenService.cs           emissão do JWT
├── Services/MapeamentoOrganizacao.cs  Usuario -> MembroResumoDto
├── wwwroot/uploads/perfis/            fotos de perfil (fora do git)
└── Migrations/

chat-web/src/
├── api/client.ts                   wrapper de fetch com Bearer, PUT/DELETE e upload
├── auth/                           AuthContext, useAuth, RotaProtegida, permissoes
├── components/Layout.tsx           casca com sidebar e topbar
└── pages/                          Login, Cadastro, Home, Perfil, Equipes
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

| Método | Rota | Autenticação | Descrição |
| --- | --- | --- | --- |
| POST | `/api/auth/registrar` | — | Cria a conta e já devolve o token |
| POST | `/api/auth/login` | — | Autentica e devolve o token |
| GET | `/api/auth/eu` | Bearer | Dados do usuário logado |
| GET | `/api/perfil` | Bearer | Dados do próprio perfil |
| PUT | `/api/perfil` | Bearer | Atualiza o nome completo |
| POST | `/api/perfil/foto` | Bearer | Upload da foto (multipart, campo `arquivo`) |
| DELETE | `/api/perfil/foto` | Bearer | Remove a foto atual |
| GET | `/api/usuarios` | Bearer | Lista resumida (para montar equipes) |
| PUT | `/api/usuarios/{id}/cargo` | Gerente+ | Define (ou tira) o cargo de alguém |
| PUT | `/api/usuarios/{id}/equipe` | Gerente+ | Lota (ou desliga) alguém de uma equipe |
| GET | `/api/cargos` | Bearer | Lista cargos |
| POST | `/api/cargos` | Gerente+ | Cria cargo |
| PUT/DELETE | `/api/cargos/{id}` | Gerente+ | Atualiza/remove um cargo |
| GET | `/api/equipes` | Bearer | Lista equipes |
| GET | `/api/equipes/arvore` | Bearer | Organograma completo |
| POST | `/api/equipes` | Gerente+ | Cria equipe |
| PUT/DELETE | `/api/equipes/{id}` | Gerente+ | Atualiza/remove uma equipe |
| POST | `/api/equipes/{id}/membros` | Gerente+ ou supervisor da equipe | Adiciona um membro |
| DELETE | `/api/equipes/{id}/membros/{usuarioId}` | Gerente+ ou supervisor da equipe | Remove um membro |

Fotos de perfil ficam em `ChatApi/wwwroot/uploads/perfis/` (fora do controle de versão) e
são servidas como arquivo estático em `/uploads/perfis/<arquivo>`.

Em desenvolvimento o Swagger UI fica em `/swagger` e o contrato OpenAPI em `/swagger/v1/swagger.json`.

---

## Permissões (RF11)

Os quatro níveis são `Diretor` (0), `Gerente` (1), `Supervisor` (2) e `Funcionario` (3) —
quanto **menor** o número, mais alto o cargo. A permissão vem do **cargo** da pessoa, não
de uma lista de papéis separada: promover alguém é trocar o cargo dele.

| Nível | O que pode |
| --- | --- |
| Diretor, Gerente | Criar/editar cargos e equipes, atribuir cargo e equipe a qualquer pessoa |
| Supervisor | Entrar e sair membros **da equipe que ele supervisiona** |
| Funcionario / sem cargo | Somente leitura da estrutura |

Duas travas explícitas em `Permissoes.PodeAtribuirCargo`: ninguém atribui um cargo acima
do próprio nível e ninguém altera o próprio cargo.

O nível é lido **do banco** a cada requisição, e não da claim do JWT: o token vive 8 horas
e uma promoção precisa valer na requisição seguinte, não no próximo login.

**Bootstrap:** banco novo ganha os quatro cargos padrão no startup (`CargosPadrao`) e a
**primeira conta cadastrada nasce Diretora** — sem isso ninguém teria permissão para
montar a estrutura inicial. Do segundo cadastro em diante, quem entra fica sem cargo até
ser lotado.

O front (`src/auth/permissoes.ts`) espelha essas regras apenas para esconder botão que a
API recusaria — quem decide é sempre o servidor.

---

## Progresso

| Data | Etapa | Situação |
| --- | --- | --- |
| 27/08 | Setup do projeto | ✅ |
| 03/09 | Autenticação base | ✅ |
| 10/09 | Perfil e estrutura organizacional | ✅ |
| 17/09 | Permissões e hierarquia | ✅ |
| 01/10 | Chat privado | — |
| 08/10 | Chat em grupo e histórico | — |
| 15/10 | Feed de notícias | — |
| 22/10 | Botão "Ciente" | — |
| 29/10 | Login com Google | — |
| 05/11 | Testes de integração | — |
| 12/11 | Ajustes finais | — |
| 19/11 | Defesa do código | — |
