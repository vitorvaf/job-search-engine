# Arquitetura (MVP)

## Componentes
1) Worker de ingestão
- Coleta por fonte (connectors)
- Normaliza e valida
- Deduplica
- Grava no Postgres
- Indexa no Meilisearch

2) API
- Exposição REST para o site e bots
- Busca/filters consultando Meilisearch + enriquecendo do Postgres

3) Distribuição
- Bot Telegram (primeiro)
- Futuro: WhatsApp/Instagram (dependências externas)

## Fluxo
[Fontes] -> (Worker: fetch/parse) -> (Normalize) -> (Dedupe) -> Postgres
                                             -> (Index) -> Meilisearch
Site/Bot -> API -> Meilisearch (search) -> Postgres (detalhes)

## Decisões iniciais
- Postgres como fonte de verdade
- Meilisearch como index de busca (simples e rápido)
- Redis para fila/agenda (ou apenas para cache no início)
