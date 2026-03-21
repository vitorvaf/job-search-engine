# Job Search Engine

Projeto com backend .NET (ingestão + API) e frontend Next.js minimalista para navegação de vagas.

## Backend

### 1. Infra local
```bash
cp .env.example .env
docker compose up -d
```

Serviços esperados:
- Postgres: `localhost:5432`
- Meilisearch: `http://localhost:7700`
- Redis: `localhost:6379`

### 2. Rodar API
```bash
dotnet run --project src/backend/Jobs.Api
```

### 3. Rodar Worker
Execução contínua:
```bash
dotnet run --project src/backend/Jobs.Worker
```

Execução única de fonte:
```bash
dotnet run --project src/backend/Jobs.Worker -- --run-once --source=InfoJobs
```

API disponível em:
- `GET /api/jobs`
- `GET /api/jobs/{id}`
- `GET /api/sources`

## Frontend (Next.js + BFF)

### 1. Variáveis
Crie `.env.local` em `src/frontend`:
```env
BACKEND_URL=http://localhost:5000
```

### 2. Instalar e executar
```bash
cd src/frontend
npm install
npm run dev
```

Frontend disponível em `http://localhost:3000`.

## BFF interno (Next Route Handlers)

O frontend **não chama o backend diretamente no client**. As chamadas passam por:
- `GET /api/jobs`
- `GET /api/jobs/[id]`
- `GET /api/sources`

Essas rotas repassam os parâmetros permitidos ao backend, validam entradas básicas e normalizam o payload para o contrato do frontend.

## Exemplo de consultas

Lista paginada com filtros:
```bash
curl "http://localhost:3000/api/jobs?page=1&pageSize=20&q=react&workMode=Remote&tags=react,nextjs&sort=recent"
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

## Testes backend
```bash
dotnet test src/backend/Jobs.sln
```

---

## Using with GitHub Copilot

Este repositório está configurado como **Copilot-ready**: instruções de contexto, prompts e convenções são injetados automaticamente no Copilot para sugestões mais precisas.

### Configuração incluída

| Arquivo | Finalidade |
|---------|-----------|
| `.github/copilot-instructions.md` | Instruções globais lidas pelo Copilot em todo contexto |
| `.github/instructions/backend.instructions.md` | Regras C# / .NET ativas para `src/backend/**` |
| `.github/instructions/frontend.instructions.md` | Regras Next.js / TypeScript ativas para `src/frontend/**` |
| `.github/instructions/testing.instructions.md` | Convenções xUnit ativas para `src/backend/Jobs.Tests/**` |
| `.github/prompts/new-source.prompt.md` | Prompt `/new-source` — gera um conector `IJobSource` completo |
| `.github/prompts/new-test.prompt.md` | Prompt `/new-test` — gera uma classe de testes xUnit completa |

### Prompts úteis (chat do Copilot)

**Criar um novo conector de fonte de vagas:**
```
/new-source
```
> Preencha os parâmetros interativos (nome, URL, formato) e o Copilot gera o conector, fixture, teste e documentação.

**Criar testes para um parser existente:**
```
/new-test
```

**Exemplos de perguntas contextuais:**

```
Como adiciono uma nova fonte de vagas no padrão IJobSource deste projeto?
```
```
Qual é o modelo de domínio JobPosting e quais campos existem?
```
```
Como funciona a deduplicação de vagas neste projeto?
```
```
Crie um endpoint GET /api/jobs/stats que retorne totais agrupados por WorkMode usando o padrão Minimal API deste projeto.
```
```
Escreva um teste xUnit para o InfoJobsHtmlParser cobrindo o caso de HTML vazio.
```
```
Adicione suporte a filtro por `company` no frontend mantendo o padrão BFF deste projeto.
```

### O que o Copilot já sabe sobre este projeto

- Nunca sugerirá EF Migrations (schema gerenciado via `schema.sql`)
- Nunca usará `Newtonsoft.Json` (projeto usa `System.Text.Json`)
- Nunca criará MVC controllers (apenas Minimal API)
- Nunca criará um diretório `pages/` no frontend (App Router exclusivamente)
- Sempre roteará chamadas do browser pelo BFF (`app/api/`)
- Sempre usará o `HttpClient` nomeado `"Sources"` para chamadas HTTP externas
