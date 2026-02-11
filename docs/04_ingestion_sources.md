# Fontes de Ingestão (guia)

## Princípios
- Priorizar fontes públicas e estáveis no MVP.
- Implementar por "connector" com interface padrão:
  - Fetch -> Parse -> Normalize -> Emit JobPosting

## Fontes recomendadas para começar
- Greenhouse
- Lever
- Workable
- Páginas de carreiras com JSON-LD (schema.org JobPosting)
- RSS/feeds quando existir
- Indeed (quando viável)

## LinkedIn (observação)
- Alto risco de bloqueio/ban e termos restritivos.
- Estratégia MVP: não depender como fonte principal.
- Se usar: capturar metadados mínimos e link, sem "quebrar o produto" se cair.
