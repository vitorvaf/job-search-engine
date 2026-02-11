# Visão do Projeto

## Objetivo
Criar um motor de busca de vagas de tecnologia que:
1) coleta vagas de múltiplas fontes,
2) normaliza e deduplica,
3) indexa para busca,
4) distribui em canais de mensagem e em um site.

## Produtos
### 1) Propagador (mensageria)
Publicação automática de vagas em:
- Telegram (primeiro MVP)
- WhatsApp / Instagram (futuro; depende de integrações)

### 2) Site de vagas
Portal simples com:
- busca
- filtros (stack, senioridade, remoto, local, empresa)
- página da vaga
- alertas (futuro)

## Usuários-alvo
- Devs buscando vagas com menos ruído (filtros e relevância)
- Comunidades que querem um feed de vagas (curado/limpo)

## Definição de sucesso (MVP)
- Ingestão diária de pelo menos 3 fontes
- Deduplicação funcionando (reduz duplicadas visíveis)
- Site com busca e filtros básicos
- Bot Telegram postando digest diário + vagas novas
