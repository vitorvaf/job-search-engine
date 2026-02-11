# Contratos de API (MVP)

Base URL: /api

## GET /api/jobs
Query params:
- q: string (busca texto)
- tags: comma-separated (ex.: dotnet,react)
- workMode: Remote|Hybrid|Onsite
- seniority: Intern|Junior|Mid|Senior|Lead|Principal
- company: string
- location: string
- postedFrom: ISO date (YYYY-MM-DD)
- page: number (default 1)
- pageSize: number (default 20)

Response 200:
{
  "page": 1,
  "pageSize": 20,
  "total": 1234,
  "items": [
    {
      "id": "uuid",
      "title": "...",
      "company": { "name": "..." },
      "locationText": "...",
      "workMode": "Remote",
      "seniority": "Mid",
      "employmentType": "CLT",
      "tags": ["dotnet","azure"],
      "postedAt": "2026-02-10T12:00:00Z",
      "capturedAt": "2026-02-10T13:00:00Z",
      "source": { "name": "Greenhouse", "url": "..." }
    }
  ]
}

## GET /api/jobs/{id}
Response 200:
{
  "id": "uuid",
  "title": "...",
  "company": { "name": "...", "website": "..." },
  "locationText": "...",
  "workMode": "Remote",
  "seniority": "Mid",
  "employmentType": "CLT",
  "salary": null,
  "descriptionText": "...",
  "tags": ["dotnet","azure"],
  "languages": ["pt-BR"],
  "source": { "name": "Greenhouse", "type": "Greenhouse", "url": "...", "sourceJobId": "123" },
  "postedAt": null,
  "capturedAt": "2026-02-10T13:00:00Z",
  "lastSeenAt": "2026-02-10T13:00:00Z",
  "status": "Active",
  "dedupe": { "fingerprint": "...", "clusterId": null },
  "metadata": { }
}

## GET /api/sources
Response 200:
[
  { "id": "uuid", "name": "Greenhouse", "type": "Greenhouse", "baseUrl": "", "enabled": true }
]

## (Futuro) POST /api/alerts
Criar alerta por filtro.
