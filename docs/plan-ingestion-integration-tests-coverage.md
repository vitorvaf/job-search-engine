# Plano: Cobertura 100% de Testes de Integração de Ingestion

O objetivo é cobrir todos os branches do `IngestionPipeline` e de todos os 7 `IJobSource`, com infraestrutura real (PostgreSQL + MeiliSearch via Testcontainers) e HTTP mockado (WireMock.Net).

---

**Fase 0 — Criar projeto `Jobs.Tests.Integration`**

1. Criar `src/backend/Jobs.Tests.Integration/Jobs.Tests.Integration.csproj` com referência a `Jobs.Infrastructure` e NuGets:
   - `Testcontainers.PostgreSql` — container PostgreSQL efêmero
   - `Testcontainers` (base) — container genérico para MeiliSearch (`getmeili/meilisearch:v1.12`)
   - `WireMock.Net` — servidor HTTP fake para simular fontes externas
   - `xunit`, `xunit.runner.visualstudio`, `coverlet.collector`
   - Inclusão dos fixtures de `tests/fixtures/` via `Content` link, igual ao `Jobs.Tests.csproj`

---

**Fase 1 — Infraestrutura de Fixtures** *(paralelo possível para as 3 partes)*

2. `Infrastructure/SharedContainerFixture.cs` — `IAsyncLifetime` que sobe **PostgreSQL** e **MeiliSearch** via Testcontainers uma única vez por suite. Expõe `ConnectionString` e `MeiliBaseUrl`.
3. `Infrastructure/WireMockFixture.cs` — sobe `WireMockServer` em porta dinâmica; métodos `StubFromFile(url, fixturePath, contentType)` e `Reset()`.
4. `Infrastructure/IntegrationCollection.cs` — `[CollectionDefinition("Integration")]` combinando ambas as fixtures via `ICollectionFixture<>`, garantindo que containers sobem **1 vez** para toda a suite.
5. `Infrastructure/IntegrationTestBase.cs` — helper que monta `ServiceCollection` com `JobsDbContext` (usando a connection string do container), `MeiliClient` apontando para o container, `AppOptions` configurável, `Fingerprint` e `IngestionPipeline`. Cada teste recebe um índice Meili único (`test_jobs_{Guid}`) para evitar conflito.

---

**Fase 2 — Testes do `IngestionPipeline`** *(depende da Fase 1)*

6. `Ingestion/IngestionPipelineTests.cs` — cobre todos os branches de `RunOnceAsync`:

   | Cenário | O que valida |
   |---|---|
   | Primeira execução | `SourceEntity` criado, `IngestionRunEntity` com status `Running` → `Success` |
   | Segunda execução | `SourceEntity` reutilizado (não duplicado no DB) |
   | Job novo | inserido no DB + indexado no Meili; `run.Inserted++` |
   | Job duplicado (mesmo `SourceJobId`) | `run.Duplicates++`, sem insert |
   | Job atualizado (descrição maior) | `ApplyUpsertRules` retorna `true`; `run.Updated++` |
   | `ApplyUpsertRules`: `ShouldReplaceSalary` | salário com mais campos preenchidos substitui |
   | `ApplyUpsertRules`: `MergeTags` | tags novas são mergeadas, sem duplicatas |
   | `ApplyUpsertRules`: `PostedAt` anterior | data mais antiga sobrepõe a atual |
   | `FindExistingAsync` por `SourceJobId` | path primário de dedup |
   | `FindExistingAsync` por `SourceUrl` | fallback quando `SourceJobId == null` |
   | `MaxItemsPerRun` respeitado | fonte retorna N+5, pipeline para em N |
   | `ExpireMissingJobsAsync` | job com `LastSeenAt < cutoff` marcado como `Expired` |
   | Exceção na fonte | `run.Status = "Failed"`, `run.Errors++`, `run.ErrorSample` preenchido |
   | `options == null` | `ResolveFetchOptions` usa defaults de `AppOptions` |

---

**Fase 3 — Testes por Fonte `FetchAsync`** *(depende da Fase 1; cada arquivo pode ser implementado em paralelo)*

Para cada fonte, o WireMock serve os fixtures existentes em `tests/fixtures/` e os novos abaixo.

7. `Ingestion/InfoJobsJobSourceIntegrationTests.cs`
   - Source desabilitado → 0 jobs
   - `SearchUrl` vazia → 0 jobs + log Warning
   - HTTP retorna body vazio → 0 jobs
   - HTML válido (`infojobs_list.html`) → 2 jobs com title, company, location, sourceJobId corretos
   - `PassesQualityGate`: job sem título descartado com log Warning
   - Detail fetch: budget > 0 + descrição ausente → GET no URL de detalhe (`infojobs_detail.html`)
   - Detail fetch: budget = 0 → sem chamada de detalhe

8. `Ingestion/StoneVagasJobSourceIntegrationTests.cs`
   - Source desabilitado / `SearchUrl` vazia / HTTP empty → 0 jobs
   - HTML válido (novo `stone_list.html`) → jobs com company = "Stone"
   - Detail fetch quando descrição ausente

9. `Ingestion/AccentureWorkdayJobSourceIntegrationTests.cs`
   - Source desabilitado / config incompleta → 0 jobs
   - Página 1 (`accenture_workday_jobs_page1.json`) → jobs extraídos
   - Paginação: página 1 + página 2 (novo `workday_jobs_page2.json`) → jobs somados
   - `MaxItems` limita travessia de páginas
   - Resposta bloqueada (HTTP 403/challenge body) → encerra sem throw
   - Detail fetch (`accenture_workday_job_detail.json`) → descrição enriquecida

10. `Ingestion/GupyCompanyJobSourceIntegrationTests.cs`
    - Source desabilitado / `CompanyBaseUrl` vazia → 0 jobs
    - Todos os endpoints candidatos retornam empty → 0 jobs + log info
    - Endpoint API JSON (`gupy_company_jobs.json`) → jobs com `SourceJobId`, title, workMode inferido
    - Endpoint HTML com `__NEXT_DATA__` (novo `gupy_next_data.html`) → jobs parseados
    - Endpoint resolvido diferente do `CompanyBaseUrl` → log info

11. `Ingestion/CorporateCareersJobSourceIntegrationTests.cs`
    - Source desabilitado / `StartUrl` vazia / HTTP empty → 0 jobs
    - HTML com JSON-LD (novo `corporate_jsonld.html`) → path `JsonLdHtmlParser`
    - HTML TOTVS sem JSON-LD (`totvs_list.html`) → path `TotvsHtmlParser`
    - Branch `IsTotvs()`: verifica detecção pelo nome da fonte
    - Detail fetch nos dois paths

12. `Ingestion/JsonLdJobSourceIntegrationTests.cs`
    - Source desabilitado / `StartUrl` vazia → 0 jobs
    - HTML sem JSON-LD → 0 jobs + log info
    - HTML com JSON-LD (`jsonld_jobs.html`) → jobs com campos todos corretos

13. `Ingestion/JsonFixtureJobSourceIntegrationTests.cs`
    - `SamplesPath` não existe → 0 jobs + log Warning
    - Diretório sem arquivos `sample_source_*.json` → 0 jobs
    - 3 fixtures válidas (`sample_source_linkedin.json`, greenhouse, indeed) → 3 jobs com `SourceType`, tags e `WorkMode` inferidos corretamente

---

**Fase 4 — Testes Unitários Faltantes** *(depende só de Jobs.Tests existente; paralelo com Fase 3)*

14. `Jobs.Tests/Ingestion/JsonLdHtmlParserTests.cs` — `ParseJobPostings` com `jsonld_jobs.html`, HTML sem JSON-LD, JSON malformado ignorado
15. `Jobs.Tests/Ingestion/TotvsHtmlParserTests.cs` — `ParseList` com `totvs_list.html`, HTML sem links válidos
16. `Jobs.Tests/Ingestion/SourceTagInfererTests.cs` — texto com múltiplas keywords, sem keywords, inputs nulos
17. `Jobs.Tests/Ingestion/SourceTypeResolverTests.cs` — valor válido, inválido → fallback, null → fallback

---

**Fase 5 — Fixtures Novas** *(pode ser feito em paralelo com qualquer fase)*

18. `tests/fixtures/stone_list.html` — HTML no formato InfoJobs/Vagas.com.br com 2 vagas válidas para Stone
19. `tests/fixtures/gupy_next_data.html` — HTML com `__NEXT_DATA__` JSON embutido contendo 2 vagas Gupy
20. `tests/fixtures/workday_jobs_page2.json` — segunda página Workday (offset=50, 2 vagas diferentes)
21. `tests/fixtures/corporate_jsonld.html` — página de carreiras corporativa com JSON-LD `JobPosting`

---

**Arquivos relevantes**

- `src/backend/Jobs.Infrastructure/Ingestion/IngestionPipeline.cs` — classe principal; todos os branches a cobrir
- `src/backend/Jobs.Infrastructure/Ingestion/InfoJobsJobSource.cs`, `GupyCompanyJobSource.cs`, `AccentureWorkdayJobSource.cs`, `CorporateCareersJobSource.cs`, `JsonLdJobSource.cs`, `StoneVagasJobSource.cs`, `JsonFixtureJobSource.cs` — `FetchAsync` por fonte
- `src/backend/Jobs.Infrastructure/Search/MeiliClient.cs` — usado real via Testcontainers
- `src/backend/Jobs.Infrastructure/DependencyInjection.cs` — referência para setup do DI nos testes
- `src/backend/Jobs.Tests/Jobs.Tests.csproj` — template para o novo `.csproj`
- `src/backend/tests/fixtures/` — fixtures existentes + destino dos 4 novos

---

**Verificação**

1. `dotnet test src/backend/Jobs.Tests.Integration --collect:"XPlat Code Coverage"` — suite de integração com cobertura
2. `dotnet test src/backend/Jobs.Tests --collect:"XPlat Code Coverage"` — suite unitária complementar
3. `reportgenerator -reports:coverage/**/*.xml -targetdir:coverage/report -reporttypes:Html` — gerar relatório HTML unificado
4. Inspecionar o relatório: **0 linhas vermelhas** em `Jobs.Infrastructure/Ingestion/` e `Jobs.Worker/`
5. Validar manualmente no Testcontainers: jobs aparecem tanto no PostgreSQL quanto no índice MeiliSearch após cada `RunOnceAsync`

---

**Decisões de infraestrutura**

- Containers sobem **1 vez** por suite (ICollectionFixture), não por teste — reduz tempo total
- Cada teste usa **índice Meili único** (`test_jobs_{Guid}`) para isolamento
- WireMock é **reiniciado entre testes** (`Reset()` no `IAsyncLifetime`) para evitar stubs residuais
- `Jobs.Tests` existente mantido para os unit tests de parser; `Jobs.Tests.Integration` exclusivo para integração
