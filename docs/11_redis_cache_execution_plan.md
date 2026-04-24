# Plano de Execucao: Redis Cache no Backend

## Objetivo

Passar a usar o Redis que ja existe na infraestrutura como cache distribuido real no backend para reduzir latencia e carga em:

- `Meilisearch` nas consultas publicas de listagem
- `Postgres` nas consultas publicas de detalhe e fontes
- instancias repetidas do `Jobs.Api` e do BFF

Este documento define apenas o plano de implementacao por arquivos.
Nenhuma fase foi implementada ainda.

## Estado Atual

- `docker-compose.yaml` sobe o servico `redis`, mas `api` e `worker` nao recebem configuracao de conexao com Redis.
- `.env.example` contem apenas `REDIS_PASSWORD`, sem consumo no backend.
- `src/backend/Jobs.Infrastructure/DependencyInjection.cs` nao registra `IDistributedCache`, `StackExchange.Redis` nem qualquer servico de cache.
- `src/backend/Jobs.Api/Program.cs` atende os endpoints diretamente com `JobsDbContext` e `MeiliClient`.
- `src/backend/Jobs.Infrastructure/Search/MeiliClient.cs` possui apenas um cache local em memoria para `EnsureIndexAsync`.
- O frontend/BFF ja faz cache de algumas chamadas publicas com `revalidate`, mas isso nao cobre chamadas diretas ao `Jobs.Api` nem substitui cache distribuido entre instancias.

## Diretrizes Fechadas

- O `Jobs.Api` continua como `Minimal API` em `Program.cs`.
- O cache inicial sera aplicado apenas a endpoints publicos e deterministas.
- Endpoints personalizados, sensiveis ou mutaveis continuam com `no-store`.
- A primeira implementacao deve usar um servico de cache no `Jobs.Infrastructure`, nao `OutputCache` do ASP.NET.
- O `worker` e o endpoint de ingestao em lote devem conseguir invalidar cache sem depender da camada web.

## Endpoints Elegiveis

### Entram no plano de cache

- `GET /api/sources`
- `GET /api/jobs/{id}`
- `GET /api/jobs`

### Ficam fora do cache compartilhado

- `GET /health`
- `POST /api/ingestion/jobs/bulk`
- `POST /api/account/*`
- `GET /api/me/favorites`
- `PUT /api/me/favorites/{jobId}`
- `DELETE /api/me/favorites/{jobId}`

## Ordem de Execucao

1. Infra e configuracao de Redis no backend
2. Abstracao de cache distribuido em `Jobs.Infrastructure`
3. Cache para `GET /api/sources`
4. Cache para `GET /api/jobs/{id}` com invalidacao por vaga
5. Cache de curta duracao para `GET /api/jobs`
6. Observabilidade, testes e hardening

## Fase 1: Infra e Configuracao

### Objetivo

Preparar `api` e `worker` para enxergar o Redis com configuracao explicita, sem ainda ligar nenhuma rota ao cache.

### Arquivos

- `docker-compose.yaml`
- `.env.example`
- `src/backend/Jobs.Infrastructure/Options/AppOptions.cs`
- `src/backend/Jobs.Infrastructure/DependencyInjection.cs`
- `src/backend/Jobs.Api/Jobs.Api.csproj`
- `src/backend/Jobs.Infrastructure/Jobs.Infrastructure.csproj`

### Mudancas planejadas por arquivo

#### `docker-compose.yaml`

- Injetar configuracao de Redis no servico `api`.
- Injetar configuracao de Redis no servico `worker`.
- Opcionalmente alinhar senha do container com a env exposta se o projeto decidir exigir autenticacao local.

#### `.env.example`

- Completar as envs do cache, por exemplo:
  - `REDIS_CONNECTION`
  - `REDIS_INSTANCE_NAME`
- Manter `REDIS_PASSWORD` apenas se a configuracao final realmente usar senha.

#### `src/backend/Jobs.Infrastructure/Options/AppOptions.cs`

- Adicionar uma secao `Cache` com:
  - `Enabled`
  - `RedisConnectionString`
  - `InstanceName`
  - `SourcesTtlSeconds`
  - `JobDetailsTtlSeconds`
  - `JobsSearchTtlSeconds`

#### `src/backend/Jobs.Infrastructure/DependencyInjection.cs`

- Registrar `AddStackExchangeRedisCache(...)`.
- Registrar a abstracao interna de cache que sera usada pela API e pela ingestao.
- Garantir comportamento seguro quando o cache estiver desabilitado.

#### `src/backend/Jobs.Api/Jobs.Api.csproj`

- Adicionar o pacote necessario para Redis distribuido se a referencia nao vier por transitividade.

#### `src/backend/Jobs.Infrastructure/Jobs.Infrastructure.csproj`

- Adicionar o pacote do provider de Redis distribuido usado pela abstracao de cache.

### Checklist

- [ ] Definir formato final das options de cache
- [ ] Definir envs de Redis para `api` e `worker`
- [ ] Registrar Redis no backend
- [ ] Garantir fallback quando cache estiver desligado

## Fase 2: Abstracao de Cache Distribuido

### Objetivo

Criar uma camada pequena e explicita de cache no `Infrastructure` para evitar espalhar serializacao, chaves e TTLs dentro de `Program.cs` e dos servicos de ingestao.

### Arquivos

- `src/backend/Jobs.Infrastructure/Cache/IDistributedAppCache.cs`
- `src/backend/Jobs.Infrastructure/Cache/DistributedAppCache.cs`
- `src/backend/Jobs.Infrastructure/Cache/CacheKeys.cs`
- `src/backend/Jobs.Infrastructure/DependencyInjection.cs`

### Mudancas planejadas por arquivo

#### `src/backend/Jobs.Infrastructure/Cache/IDistributedAppCache.cs`

- Definir operacoes minimas:
  - `GetAsync<T>`
  - `SetAsync<T>`
  - `GetOrCreateAsync<T>`
  - `RemoveAsync`

#### `src/backend/Jobs.Infrastructure/Cache/DistributedAppCache.cs`

- Implementar sobre `IDistributedCache`.
- Usar `System.Text.Json` para serializacao.
- Centralizar TTL, tratamento de miss e remocao de chaves.

#### `src/backend/Jobs.Infrastructure/Cache/CacheKeys.cs`

- Padronizar chaves de cache, por exemplo:
  - `api:sources:v1`
  - `api:job:{id}:v1`
  - `api:jobs-search:v1:{hash}`

#### `src/backend/Jobs.Infrastructure/DependencyInjection.cs`

- Registrar a implementacao concreta da abstracao criada.

### Checklist

- [ ] Criar abstracao de cache pequena e reutilizavel
- [ ] Definir formato de serializacao
- [ ] Definir convencao de chaves e versionamento
- [ ] Registrar a implementacao em DI

## Fase 3: Cache de `GET /api/sources`

### Objetivo

Comecar pelo endpoint publico mais simples e estavel para validar integracao com Redis antes de tocar os caminhos mais quentes.

### Arquivos

- `src/backend/Jobs.Api/Program.cs`
- `src/backend/Jobs.Infrastructure/Options/AppOptions.cs`
- `src/backend/Jobs.Tests/` com testes do endpoint

### Mudancas planejadas por arquivo

#### `src/backend/Jobs.Api/Program.cs`

- Injetar a abstracao de cache no handler de `GET /api/sources`.
- Trocar a leitura direta por `GetOrCreateAsync` com chave unica.
- Aplicar `AsNoTracking()` na query, pois o endpoint e somente leitura.

#### `src/backend/Jobs.Infrastructure/Options/AppOptions.cs`

- Consumir o TTL especifico para fontes.

#### `src/backend/Jobs.Tests/...`

- Adicionar cobertura para hit, miss e expiracao basica.

### TTL inicial sugerido

- `5` a `15` minutos

### Checklist

- [ ] Cachear `GET /api/sources`
- [ ] Garantir que a resposta nao mude de contrato
- [ ] Cobrir hit e miss em testes

## Fase 4: Cache de `GET /api/jobs/{id}` com Invalidacao por Vaga

### Objetivo

Cachear a leitura de detalhe por vaga, que e publica, determinista e permite invalidacao direcionada.

### Arquivos

- `src/backend/Jobs.Api/Program.cs`
- `src/backend/Jobs.Infrastructure/Ingestion/IngestionPipeline.cs`
- `src/backend/Jobs.Infrastructure/BulkIngestion/BulkJobIngestionService.cs`
- `src/backend/Jobs.Infrastructure/Cache/CacheKeys.cs`
- `src/backend/Jobs.Tests/` com testes de endpoint e invalidacao

### Mudancas planejadas por arquivo

#### `src/backend/Jobs.Api/Program.cs`

- Cachear `GET /api/jobs/{id}` por `id`.
- Aplicar `AsNoTracking()` na consulta do detalhe.
- Manter `404` fora do cache ou com TTL curto, conforme decisao final.

#### `src/backend/Jobs.Infrastructure/Ingestion/IngestionPipeline.cs`

- Invalidar a chave de detalhe da vaga apos insert, update ou expire bem-sucedido.
- Evitar invalidacao em caminhos que nao alteram os dados finais da vaga.

#### `src/backend/Jobs.Infrastructure/BulkIngestion/BulkJobIngestionService.cs`

- Invalidar a chave de detalhe da vaga em inserts e updates bem-sucedidos.
- Reaproveitar a mesma convencao de chaves da pipeline do worker.

#### `src/backend/Jobs.Infrastructure/Cache/CacheKeys.cs`

- Expor helper para chave por `jobId`.

### TTL inicial sugerido

- `1` a `5` minutos

### Checklist

- [ ] Cachear `GET /api/jobs/{id}`
- [ ] Invalidar detalhe apos escrita relevante
- [ ] Cobrir insert, update e expire em testes

## Fase 5: Cache de `GET /api/jobs`

### Objetivo

Proteger o hotspot mais caro do backend com TTL curto e chave canonica por query, sem depender de invalidacao agressiva no primeiro momento.

### Arquivos

- `src/backend/Jobs.Api/Program.cs`
- `src/backend/Jobs.Infrastructure/Cache/CacheKeys.cs`
- `src/backend/Jobs.Infrastructure/Search/MeiliClient.cs`
- `src/backend/Jobs.Tests/` com testes de normalizacao de chave

### Mudancas planejadas por arquivo

#### `src/backend/Jobs.Api/Program.cs`

- Extrair a normalizacao dos parametros da busca para compor uma chave estavel.
- Cachear o payload final do endpoint, nao apenas a resposta crua do Meili.
- Garantir que `page` e `pageSize` facam parte da chave.

#### `src/backend/Jobs.Infrastructure/Cache/CacheKeys.cs`

- Adicionar helper para chaves de busca com hash da query normalizada.

#### `src/backend/Jobs.Infrastructure/Search/MeiliClient.cs`

- Nenhuma mudanca obrigatoria na primeira entrega.
- Opcionalmente expor metadados futuros para observabilidade da busca, se isso ajudar a medir ganho de cache.

### TTL inicial sugerido

- `30` a `60` segundos

### Restricoes conhecidas

- O endpoint lista resultados do `Meilisearch`, nao do Postgres.
- `IngestionPipeline` e `BulkJobIngestionService` gravam no banco e depois fazem upsert no Meili.
- Como a indexacao do Meili nao e aguardada ate ficar consultavel, a primeira versao deve preferir TTL curto em vez de invalidacao ampla.

### Checklist

- [ ] Canonicalizar query string para chave de cache
- [ ] Cachear payload final da busca
- [ ] Cobrir equivalencia de queries em testes
- [ ] Manter TTL curto na primeira entrega

## Fase 6: Observabilidade e Hardening

### Objetivo

Medir hit ratio, garantir seguranca do escopo de cache e validar a operacao em ambiente local e CI.

### Arquivos

- `src/backend/Jobs.Api/Program.cs`
- `src/backend/Jobs.Infrastructure/Cache/DistributedAppCache.cs`
- `src/backend/Jobs.Tests/`
- `README.md`
- `docs/03_architecture.md`

### Mudancas planejadas por arquivo

#### `src/backend/Jobs.Infrastructure/Cache/DistributedAppCache.cs`

- Adicionar logs e contadores simples de `hit`, `miss`, `set` e `remove`.

#### `src/backend/Jobs.Api/Program.cs`

- Garantir que endpoints sensiveis continuem com `no-store`.
- Evitar compartilhamento acidental em rotas user-specific.

#### `README.md`

- Atualizar a secao de setup se novas envs de Redis forem obrigatorias.

#### `docs/03_architecture.md`

- Atualizar a descricao de Redis de possibilidade para uso efetivo apos a implementacao.

### Checklist

- [ ] Medir `hit/miss` por endpoint cacheado
- [ ] Revisar rotas que devem continuar fora do cache
- [ ] Atualizar docs operacionais apos a entrega

## Estrutura Sugerida de Novos Arquivos

- `src/backend/Jobs.Infrastructure/Cache/IDistributedAppCache.cs`
- `src/backend/Jobs.Infrastructure/Cache/DistributedAppCache.cs`
- `src/backend/Jobs.Infrastructure/Cache/CacheKeys.cs`

Se surgir necessidade de separar leitura e invalidacao, manter isso dentro da mesma pasta `Cache/` e evitar espalhar helpers por varias camadas.

## Riscos Conhecidos

- Divergencia temporaria entre `Postgres` e `Meilisearch` apos writes.
- Chave de busca com cardinalidade alta em `GET /api/jobs`.
- Risco de cache compartilhado em endpoints com contexto de usuario se a selecao de rotas nao for estrita.
- Ganho parcial ja absorvido pelo cache do BFF, o que exige medir o beneficio real no `Jobs.Api`.

## Validacao Planejada

- `dotnet test src/backend/Jobs.sln`
- Validar localmente `docker compose up -d` com Redis acessivel para `api` e `worker`
- Testar hit e miss manualmente nos endpoints publicos cacheados
- Confirmar que endpoints de conta e favoritos permanecem com `Cache-Control: no-store`

## Decisao Final Recomendada

Implementar em tres ondas praticas:

1. Infra + abstracao + `GET /api/sources`
2. `GET /api/jobs/{id}` + invalidacao por vaga
3. `GET /api/jobs` com TTL curto e observabilidade

Essa ordem entrega valor cedo, reduz risco e preserva a possibilidade de revisar a estrategia de invalidacao antes de cachear o endpoint mais sensivel a consistencia.
