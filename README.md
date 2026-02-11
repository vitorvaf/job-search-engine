# Job Search Engine (Tech Jobs)

Motor de busca de vagas com coleta multi-fontes, normalização, deduplicação, indexação e distribuição:
- Distribuição: bots/canais (Telegram/WhatsApp/etc) + site
- Fontes: várias (inicialmente fontes públicas/amigáveis)

## Rodar local (infra)
1. Copie o `.env.example` para `.env` e ajuste se quiser
2. Suba os serviços:
   ```bash
   docker compose up -d
