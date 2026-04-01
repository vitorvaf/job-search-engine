# Job Search Engine

Projeto com backend .NET (ingestão + API) e frontend Next.js minimalista para navegação de vagas.

## Backend

### 1. Infra local
```bash
cp .env.example .env
docker compose up -d
```

O arquivo `.env` na raiz é opcional: `docker-compose.yaml` já possui defaults equivalentes.

Serviços esperados:
- Postgres: `localhost:5432`
- Meilisearch: `http://localhost:7700`
- Redis: `localhost:6379`
- Adminer: `http://localhost:8080`

### 2. Rodar API
```bash
dotnet run --project src/backend/Jobs.Api
```

API disponível em `http://localhost:5004`.

Em ambiente `Development`, o Swagger fica em `http://localhost:5004/swagger`.

### 3. Rodar Worker
Execução contínua:
```bash
dotnet run --project src/backend/Jobs.Worker
```

Execução única de uma fonte:
```bash
dotnet run --project src/backend/Jobs.Worker -- --run-once --source=InfoJobs
```

### 4. Endpoints principais
- `GET /api/jobs`
- `GET /api/jobs/{id}`
- `GET /api/sources`

## Frontend (Next.js + BFF)

### 1. Variáveis
```bash
cp src/frontend/.env.local.example src/frontend/.env.local
```

Valor padrão:
```env
BACKEND_URL=http://localhost:5004
```

`BACKEND_URL` é usado apenas pelo BFF server-side do Next.js.

### 2. Instalar e executar
```bash
cd src/frontend
npm install
npm run dev
```

Frontend disponível em `http://localhost:3000`.

## BFF interno (Next Route Handlers)

O frontend não chama o backend diretamente no browser. As chamadas passam por:
- `GET /api/jobs`
- `GET /api/jobs/[id]`
- `GET /api/sources`

Essas rotas:
- validam e saneiam query params
- chamam `Jobs.Api` via `src/frontend/lib/api-proxy.ts`
- normalizam o payload em `src/frontend/lib/normalizers.ts`
- entregam ao UI o modelo do frontend definido em `src/frontend/lib/types.ts`

## Exemplo de consultas

Lista paginada com filtros:
```bash
curl "http://localhost:3000/api/jobs?page=1&pageSize=20&q=react&workMode=Remote&tags=react,nextjs&sort=postedAt:desc"
```

Detalhe:
```bash
curl "http://localhost:3000/api/jobs/00000000-0000-0000-0000-000000000000"
```

Fontes:
```bash
curl "http://localhost:3000/api/sources"
```

## Rotas da interface

- `/` lista de vagas com busca, filtros e paginação
- `/vagas/[id]` detalhe da vaga (SSR)
- `/favoritos` favoritos via `localStorage`
- `/sobre` resumo do projeto

## Validação local

Backend:
```bash
dotnet test src/backend/Jobs.sln
```

Frontend:
```bash
node scripts/check-boundary-drift.mjs

cd src/frontend
npm run lint
npm run build
```

---

## Using with GitHub Copilot

Este repositório está configurado como Copilot-ready: instruções de contexto, prompts e convenções foram alinhados com o código atual para reduzir drift entre sugestões e implementação real.

### Configuração incluída

| Arquivo | Finalidade |
|---------|-----------|
| `.github/copilot-instructions.md` | Instruções globais lidas pelo Copilot em todo contexto |
| `.github/instructions/backend.instructions.md` | Regras C# / .NET ativas para `src/backend/**` |
| `.github/instructions/frontend.instructions.md` | Regras Next.js / TypeScript ativas para `src/frontend/**` |
| `.github/instructions/testing.instructions.md` | Convenções xUnit ativas para `src/backend/Jobs.Tests/**` |
| `.github/prompts/new-source.prompt.md` | Prompt `/new-source` para adicionar ou configurar uma nova fonte de ingestão |
| `.github/prompts/new-test.prompt.md` | Prompt `/new-test` para gerar ou expandir testes xUnit |

### Prompts úteis

**Criar ou adaptar uma nova fonte de vagas:**
```
/new-source
```

**Criar testes para um parser ou serviço existente:**
```
/new-test
```

**Exemplos de perguntas contextuais:**

```
Esta fonte cabe em CorporateCareers, JsonLd ou GupyCompanies, ou precisa de um novo IJobSource?
```
```
Quais arquivos preciso revisar se eu mudar filtros, sort ou paginação da API?
```
```
Como funciona a deduplicação de vagas neste projeto?
```
```
Escreva um teste xUnit para o GupyJobsJsonParser usando a fixture gupy_company_jobs.json.
```
```
Adicione suporte a um novo campo no frontend mantendo o boundary BFF deste projeto.
```

### O que o Copilot já sabe sobre este projeto

- Não sugerir EF Migrations; o schema vive em `schema.sql`
- Não usar `Newtonsoft.Json`; o projeto usa `System.Text.Json`
- Não criar MVC controllers; `Jobs.Api` usa Minimal API em `Program.cs`
- Não criar `pages/` no frontend; apenas App Router
- Não chamar `Jobs.Api` direto do browser; usar o BFF em `app/api/`
- Não presumir que toda fonte nova exige um `IJobSource`; primeiro revisar as famílias já existentes

---

## Using with Claude Code

Este repositório também está configurado para Claude Code com memória compartilhada, regras por stack, permissões do time e slash commands alinhados ao fluxo real do projeto.

### Configuração incluída

| Arquivo | Finalidade |
|---------|-----------|
| `CLAUDE.md` | Memória compartilhada do projeto carregada na sessão |
| `.claude/settings.json` | Permissões compartilhadas e proteções contra leitura de segredos/artefatos gerados |
| `.claude/rules/backend.md` | Regras de backend para `src/backend/**` |
| `.claude/rules/frontend.md` | Regras de frontend para `src/frontend/**` |
| `.claude/rules/testing.md` | Regras de testes e fixtures |
| `.claude/commands/new-source.md` | Slash command `/new-source` para adicionar/configurar uma nova fonte |
| `.claude/commands/new-test.md` | Slash command `/new-test` para expandir cobertura xUnit |
| `.claude/commands/trace-job-flow.md` | Slash command `/trace-job-flow` para rastrear o fluxo da vaga ponta a ponta |
| `.claude/commands/review-boundary.md` | Slash command `/review-boundary` para revisar impacto cross-stack |
| `.claude/agents/parser-reviewer.md` | Subagente de revisão de fontes, parsers e fixtures |
| `.claude/agents/boundary-reviewer.md` | Subagente de revisão de contrato backend/BFF/frontend |
| `.claude/hooks/*.mjs` | Hooks leves de lembrete após edições em boundary e ingestão |
| `.claude/settings.local.example.json` | Exemplo opcional para fluxo local mais rápido |
| `scripts/check-boundary-drift.mjs` | Guarda de CI para enums, filtros, sorts, query params e URLs backend/frontend |

### Comandos úteis no Claude Code

```bash
claude
/new-source Greenhouse https://boards.greenhouse.io json
/new-test GupyJobsJsonParser gupy_company_jobs.json json
/trace-job-flow GupyExample
/review-boundary "alteracao em filtros e sort da listagem"
```

### Fluxo recomendado

1. Se quiser um fluxo mais rápido, mescle `.claude/settings.local.example.json` no seu `.claude/settings.local.json`.
2. Use `/memory` para editar `CLAUDE.md`.
3. Use `/permissions` e `/config` para inspecionar o comportamento ativo.
4. Use `/trace-job-flow` quando precisar entender o caminho completo de uma vaga, ou `/review-boundary` antes de fechar mudanças cross-stack.
5. Os hooks do projeto adicionam lembretes leves quando você edita arquivos de ingestão ou boundary; trate-os como guardrails, não como substituto para docs e revisão.
6. Quando as convenções do projeto mudarem, mantenha `.claude/` e `.github/` alinhados.
