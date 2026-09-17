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
├── Controllers/UsuariosController.cs  listagem de pessoas e atribuição de cargo
├── Data/AppDbContext.cs               mapeamento das entidades
├── Data/AppDbContextFactory.cs        usado só pelo dotnet ef
├── Data/SeedCargos.cs                 cria os 4 cargos padrão numa base nova
├── Models/                            Usuario, Cargo, Equipe, Sala,
│                                       SalaUsuario, Mensagem, Postagem, Ciencia
├── Services/TokenService.cs           emissão do JWT
├── Services/Permissoes.cs             quem pode o quê (RF11)
├── Services/MapeamentoOrganizacao.cs  Usuario -> MembroResumoDto
├── wwwroot/uploads/perfis/            fotos de perfil (fora do git)
└── Migrations/

chat-web/src/
├── api/client.ts                   wrapper de fetch com Bearer, PUT/DELETE e upload
├── auth/                           AuthContext, useAuth, RotaProtegida
├── components/Layout.tsx           casca com sidebar e topbar
├── lib/permissoes.ts               espelho das regras do backend, só para a UI
└── pages/                          Login, Cadastro, Home, Perfil, Equipes, Pessoas
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

"Gerente" na coluna significa **gerente ou acima** — a hierarquia é cumulativa.
¹ O gerente só distribui cargos de Supervisor para baixo, e não altera quem já é
diretor ou gerente. ² Ou o supervisor daquela equipe específica.

## Permissões (RF11)

```
                 cargos   equipes    membros           atribuir cargo
Diretor           CRUD     CRUD      qualquer equipe   qualquer nível
Gerente           ler      CRUD      qualquer equipe   até Supervisor
Supervisor        ler      ler       só a sua equipe   não
Funcionário       ler      ler       não               não
```

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
assume a diretoria. Pelo mesmo motivo o sistema recusa tirar o cargo do último diretor.

Fotos de perfil ficam em `ChatApi/wwwroot/uploads/perfis/` (fora do controle de versão) e
são servidas como arquivo estático em `/uploads/perfis/<arquivo>`.

Em desenvolvimento o Swagger UI fica em `/swagger` e o contrato OpenAPI em `/swagger/v1/swagger.json`.

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
