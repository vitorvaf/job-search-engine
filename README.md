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
