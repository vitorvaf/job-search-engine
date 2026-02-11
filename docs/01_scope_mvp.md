# Escopo do MVP

## Entra no MVP
- Coleta de fontes públicas/amigáveis (ex.: Greenhouse/Lever/Workable, páginas de carreiras, Indeed quando viável)
- Normalização para um modelo único (JobPosting)
- Deduplicação simples + melhoria incremental
- Indexação no Meilisearch
- API REST para leitura (site/bot)
- Bot Telegram (canal) com:
  - post por vaga
  - digest diário (top N)

## Não entra no MVP (agora)
- Dependência forte de LinkedIn (risco alto)
- Login/conta por usuário com preferências avançadas
- Recomendação personalizada complexa
- WhatsApp/Instagram (integrações e regras variam)
- Pagamentos/assinaturas

## Regras do jogo
- Se uma fonte ficar instável, o sistema deve degradar sem quebrar o todo.
- Sempre armazenar:
  - fonte + URL original + timestamps (capturado/atualizado/removido)
