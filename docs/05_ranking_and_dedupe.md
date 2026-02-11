# Deduplicação e Ranking

## Deduplicação (MVP)
### Objetivo
Agrupar a mesma vaga que aparece em múltiplas fontes.

### Fingerprint determinístico (primeira versão)
fingerprint = hash(
  normalize(companyName) +
  normalize(title) +
  normalize(locationText) +
  normalize(workMode)
)

normalize():
- lowercase
- remove acentos
- remove pontuação
- trim
- colapsa espaços

### Similaridade (versão 2)
- usar similaridade de título + empresa + descrição (ex.: Jaccard/TF-IDF)
- clusterId para unir duplicadas

## Ranking (MVP)
Score simples:
- Recência (postedAt/capturedAt)
- Completude (tem descrição, tags, workMode)
- Match por filtros do usuário (se existir)
- Penalidade para "spam" (descrição curta demais, título genérico demais)

## Anti-spam (regras iniciais)
- Títulos proibidos: "Vaga", "Oportunidade", etc (sem contexto)
- Descrição < 200 chars => baixa prioridade
