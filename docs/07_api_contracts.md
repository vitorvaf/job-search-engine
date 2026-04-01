# Contratos de API e BFF

Este projeto tem duas camadas de contrato:

1. o payload bruto exposto por `Jobs.Api`
2. o payload normalizado pelo BFF do Next.js para a UI

O contrato bruto é definido no código de `src/backend/Jobs.Api/Program.cs`.

Guardrail automatizado:
- `node scripts/check-boundary-drift.mjs` valida enums, sorts, query params e referências locais de `BACKEND_URL` entre backend, frontend, docs e CI.

## Backend REST API

Base URL: `/api`

### GET /api/jobs

Query params aceitos pelo backend:
- `q`
- `tags` em CSV
- `workMode`: `Remote | Hybrid | Onsite`
- `seniority`: `Intern | Junior | Mid | Senior | Staff | Lead | Principal`
- `employmentType`: `CLT | PJ | Contractor | Internship | Temporary`
- `sourceName`
- `company`
- `location`
- `postedFrom` em formato ISO
- `sort`
- `page`
- `pageSize`

Sorts reconhecidos explicitamente hoje:
- `postedAt:asc`
- `capturedAt:asc`
- `capturedAt:desc`

Se `sort` vier vazio ou com valor desconhecido, o backend faz fallback para:
- `postedAt:desc`
- `capturedAt:desc`

Response 200:

```json
{
  "page": 1,
  "pageSize": 20,
  "total": 1234,
  "items": [
    {
      "id": "uuid",
      "title": "Software Engineer",
      "company": { "name": "Acme" },
      "locationText": "Sao Paulo, SP",
      "workMode": "Remote",
      "seniority": "Mid",
      "employmentType": "CLT",
      "tags": ["dotnet", "azure"],
      "postedAt": "2026-02-10T12:00:00Z",
      "capturedAt": "2026-02-10T13:00:00Z",
      "source": { "name": "Gupy", "url": "https://example.com/job/123" }
    }
  ]
}
```

### GET /api/jobs/{id}

Response 200:

```json
{
  "id": "uuid",
  "title": "Software Engineer",
  "company": {
    "name": "Acme",
    "website": "https://acme.example",
    "industry": "Technology"
  },
  "locationText": "Sao Paulo, SP",
  "location": {
    "country": "BR",
    "state": "SP",
    "city": "Sao Paulo"
  },
  "workMode": "Remote",
  "seniority": "Mid",
  "employmentType": "CLT",
  "salary": {
    "min": 12000,
    "max": 18000,
    "currency": "BRL",
    "period": "month"
  },
  "descriptionText": "Job description...",
  "tags": ["dotnet", "azure"],
  "languages": ["pt-BR"],
  "source": {
    "name": "Gupy",
    "type": "Gupy",
    "url": "https://example.com/job/123",
    "sourceJobId": "123"
  },
  "postedAt": "2026-02-10T12:00:00Z",
  "capturedAt": "2026-02-10T13:00:00Z",
  "lastSeenAt": "2026-02-10T13:00:00Z",
  "status": "Active",
  "dedupe": {
    "fingerprint": "abc",
    "clusterId": null
  },
  "metadata": {}
}
```

### GET /api/sources

Response 200:

```json
[
  {
    "id": "uuid",
    "name": "GupyExample",
    "type": "Gupy",
    "baseUrl": "https://example.gupy.io/jobs",
    "enabled": true
  }
]
```

## BFF do frontend

As rotas em `src/frontend/app/api/` não expõem o payload bruto do backend diretamente para a UI.

Responsabilidades do BFF:
- validar e saneiar query params
- chamar `Jobs.Api`
- normalizar o payload com `src/frontend/lib/normalizers.ts`
- devolver o modelo usado pelo UI em `src/frontend/lib/types.ts`

### Modelo normalizado usado pela UI

Lista e detalhe convergem para um modelo enxuto com campos como:
- `id`
- `title`
- `company` como `string`
- `location` como `string`
- `workMode`
- `seniority`
- `employmentType`
- `tags`
- `description`
- `applyUrl`
- `sourceName`
- `postedAt`

Consequência prática:
- mudanças em `Jobs.Api/Program.cs` não devem ser copiadas automaticamente para a UI
- mudanças de contrato precisam revisar `api-proxy.ts`, `normalizers.ts`, `types.ts`, `constants.ts` e os componentes afetados
