# Modelo de Domínio (MVP)

## Entidade principal: JobPosting (normalizada)

### Campos mínimos
- id (UUID) - gerado internamente
- source: { name, type, sourceJobId?, url }
- title
- company: { name, website?, industry? }
- locationText (string livre)
- location: { country?, state?, city? } (opcional)
- workMode: Remote | Hybrid | Onsite | Unknown
- seniority: Intern | Junior | Mid | Senior | Staff | Lead | Principal | Unknown
- employmentType: CLT | PJ | Contractor | Internship | Temporary | Unknown
- salary: { min?, max?, currency?, period? } (opcional)
- descriptionText (texto limpo)
- tags: string[] (ex.: ["dotnet","react","aws","kafka"])
- languages: string[] (ex.: ["pt-BR","en"])
- postedAt (data da vaga na fonte, se existir)
- capturedAt (data em que capturamos)
- lastSeenAt (última vez que vimos a vaga na coleta)
- status: Active | Expired | Unknown
- dedupe:
  - fingerprint (hash determinístico)
  - clusterId? (para agrupar duplicadas)
- metadata (json) - payload bruto resumido / campos extras

## Entidades auxiliares (MVP)
### Source
- id, name, type (LinkedIn, Greenhouse, Lever, Indeed, CareersPage)
- baseUrl
- enabled
- rateLimitPolicy (json)

### IngestionRun
- id, sourceId, startedAt, finishedAt, status
- counters: fetched, parsed, normalized, indexed, duplicates, errors
- errorSample (texto)

## Observação
- "metadata" guarda extras sem travar o schema.
- O index (Meilisearch) usa um "documento de busca" derivado do JobPosting.
