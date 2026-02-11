# Regras do Projeto (Constituição)

## Fonte de verdade
- Docs em /docs são a especificação do sistema.
- O modelo normalizado JobPosting é a linguagem comum entre ingestão, indexação e distribuição.

## Padrões
- Sem “scraping agressivo” como dependência do MVP.
- Cada fonte é um connector isolado.
- Worker deve ser idempotente (rodar N vezes sem duplicar bagunça).

## Qualidade
- Toda nova fonte deve ter:
  - amostra em /docs/samples
  - testes para parse/normalize
  - rate limit + retries

## Done (para tasks)
- Logs básicos
- Contadores (fetched/parsed/normalized/indexed/duplicates/errors)
- Pelo menos 1 teste cobrindo o caminho feliz
