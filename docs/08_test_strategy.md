# Estratégia de Testes

## Unitários (prioridade)
- Normalização (normalize string)
- Fingerprint (hash consistente)
- Parsing de fontes (inputs sample -> output JobPosting)
- Regras anti-spam

## Integração
- Indexação no Meilisearch (subir docker)
- Persistência no Postgres

## Dados de teste
Usar /docs/samples como fixtures.
