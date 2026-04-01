# Fontes de Ingestão

Este repositório já tem mais de um padrão de fonte. Antes de criar um novo conector, descubra se a nova origem cabe em uma família existente.

## Pipeline real do projeto

```text
source config or connector
  -> fetch
  -> parse
  -> JobPosting
  -> IngestionPipeline
  -> PostgreSQL + Meilisearch
```

Os pontos centrais do fluxo ficam em:
- `src/backend/Jobs.Infrastructure/Ingestion/IJobSource.cs`
- `src/backend/Jobs.Infrastructure/Ingestion/IngestionPipeline.cs`
- `src/backend/Jobs.Worker/Worker.cs`

## Famílias já existentes

### 1. Fontes dedicadas

Usadas quando o comportamento de fetch e parse é específico o bastante para justificar um conector próprio.

Exemplos atuais:
- `InfoJobsJobSource`
- `StoneVagasJobSource`
- `AccentureWorkdayJobSource`
- `JsonFixtureJobSource`

### 2. Fontes configuradas por lista em `App:Sources`

Usadas quando várias empresas seguem o mesmo padrão estrutural e só mudam URLs ou limites.

Famílias atuais:
- `CorporateCareers`
- `JsonLd`
- `GupyCompanies`

Essas famílias são registradas dinamicamente em `DependencyInjection.cs` com base em `AppOptions`.

## Como decidir entre configuração e novo conector

### Prefira reaproveitar uma família existente quando

- a paginação segue o mesmo padrão já implementado
- o parse usa a mesma estrutura HTML/JSON de uma família atual
- só mudam URL base, nome, limites ou detalhes de navegação

### Crie um novo `IJobSource` quando

- a autenticação ou o protocolo de fetch é diferente
- a paginação é incompatível com as famílias existentes
- o parse exige uma estratégia nova
- a fonte precisa de um parser dedicado que não se encaixa nas implementações atuais

## Checklist mínimo para uma nova fonte

1. Reutilizar parser/família existente ou criar a nova implementação em `src/backend/Jobs.Infrastructure/Ingestion/`.
2. Atualizar `src/backend/Jobs.Infrastructure/Options/AppOptions.cs` quando houver configuração nova.
3. Manter `appsettings.json` e `appsettings.Development.json` alinhados em `Jobs.Api` e `Jobs.Worker`.
4. Adicionar fixture em `src/backend/tests/fixtures/`.
5. Adicionar ou expandir testes em `src/backend/Jobs.Tests/Ingestion/`.
6. Atualizar documentação relevante em `docs/` quando a nova fonte introduzir um padrão novo.

## O que evitar

- presumir que toda fonte nova exige um novo `IJobSource`
- duplicar lógica de parser já existente
- colocar regras de pipeline dentro do conector em vez de reutilizar `IngestionPipeline`
- fazer scraping agressivo ou depender de fluxos claramente anti-bot no MVP
