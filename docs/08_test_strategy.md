# Estratégia de Testes

Este repositório ainda não tem uma pirâmide de testes completa. A cobertura automatizada hoje está concentrada em parsing, normalização de texto e validação por fixtures.

## O que já existe

### Backend unitário e fixture-based

Local: `src/backend/Jobs.Tests/`

Cobertura atual:
- `Fingerprint`
- `JobTextNormalizer`
- parsers HTML/JSON como `InfoJobsHtmlParser`, `JsonLdHtmlParser`, `TotvsHtmlParser`, `WorkdayJobsJsonParser`, `GupyJobsJsonParser`
- validação de fontes contra fixtures mais realistas em `IngestionSourceValidationTests`

Fixtures:
- ficam em `src/backend/tests/fixtures/`
- são copiadas para o output via `Jobs.Tests.csproj`

### Frontend

Hoje o frontend é validado principalmente por:
- `npm run lint`
- `npm run build`

Não há, neste momento, suíte dedicada de testes unitários ou e2e para o frontend.

## O que a CI executa

`.github/workflows/ci.yml` roda:
- restore, build e `dotnet test` do backend
- `node scripts/check-boundary-drift.mjs` para guardar enums, filters, query params, sorts e URLs locais do boundary backend/frontend
- `npm ci`, `npm run lint` e `npm run build` do frontend

## Como escolher o teste certo

### Ao mexer em parser ou fonte

Adicionar ou expandir:
- fixture em `src/backend/tests/fixtures/`
- teste dedicado em `src/backend/Jobs.Tests/Ingestion/`
- quando houver resposta capturada do mundo real, ampliar `IngestionSourceValidationTests`

### Ao mexer em normalização, fingerprint ou helpers de ingestão

Adicionar ou expandir testes unitários puros, sem HTTP nem banco.

### Ao mexer em API, filtros, sort, paginação ou enums

Hoje não há contract tests dedicados para esse boundary.

Mínimo esperado:
- revisar `docs/07_api_contracts.md`
- validar manualmente o BFF do frontend
- rodar `npm run lint` e `npm run build`

### Ao mexer em UI

Mínimo esperado:
- `npm run lint`
- `npm run build`
- validação manual da tela ou rota afetada

## O que ainda falta

Ainda não existem suítes dedicadas para:
- `IngestionPipeline`
- `Jobs.Api`
- `Jobs.Worker`
- route handlers do BFF
- testes unitários/e2e do frontend

Essas camadas continuam como oportunidade de evolução, mas não devem ser prometidas como cobertura já existente.
