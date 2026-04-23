<div align="center">

# 🔍 Job Search Engine

### Um motor open source para descoberta e distribuição inteligente de vagas de tecnologia

[![Build](https://img.shields.io/github/actions/workflow/status/vitorvaf/job-search-engine/ci.yml?branch=main&label=build&style=flat-square)](https://github.com/vitorvaf/job-search-engine/actions)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple?style=flat-square)](https://dotnet.microsoft.com)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen?style=flat-square)](CONTRIBUTING.md)

**Agregue vagas de múltiplas fontes, normalize, deduplique e distribua — onde e como quiser.**

[Como começar](#-como-começar) · [Funcionalidades](#-funcionalidades) · [Arquitetura](#-arquitetura) · [Contribuir](#-contribuindo)

</div>

---

## O problema que resolvemos

Se você já perdeu uma boa oportunidade porque ela estava enterrada na página 5 de um site de vagas, ou recebeu a mesma vaga cinco vezes em canais diferentes, você entende o problema.

Vagas de tecnologia estão espalhadas por dezenas de plataformas — sites corporativos, ATSs como Gupy e Workday, agregadores genéricos. Cada uma com seu formato, sua estrutura, seu ruído. Não existe um lugar centralizado, limpo e customizável que funcione do jeito que desenvolvedores querem.

**Este projeto existe para mudar isso.**

---

## A solução

Job Search Engine é uma plataforma open source que:

- **Coleta** vagas automaticamente de múltiplas fontes em segundo plano
- **Normaliza** tudo em um modelo de domínio padronizado — mesmos campos, mesmos enums, mesma estrutura
- **Deduplica** usando fingerprinting inteligente, eliminando a mesma vaga que aparece em cinco lugares diferentes
- **Indexa** no Meilisearch para buscas rápidas e relevantes
- **Expõe** uma API REST que você usa como quiser: site, bot, integração, o que precisar

É a infraestrutura que você precisaria construir do zero — já construída, testada e extensível.

---

## ✨ Funcionalidades

- 🔎 **Coleta automática** de vagas via worker em background
- 🧩 **Multi-fonte** — conectores para sites corporativos, Gupy, Workday, InfoJobs e mais
- 🧠 **Normalização inteligente** com suporte a enriquecimento por IA (tags, senioridade)
- ♻️ **Deduplicação por fingerprint** — sem duplicatas nas buscas
- ⚡ **Indexação rápida** com Meilisearch (busca full-text, filtros, facets)
- 🔗 **API REST centralizada** pronta para consumo
- 📥 **Endpoint de ingestão bulk** para integração com n8n, Firecrawl e automações externas
- 🤖 **Distribuição extensível** — arquitetado para bots Telegram, WhatsApp e outros canais
- 🖥️ **Frontend incluído** — Next.js com busca, filtros, paginação e detalhe da vaga
- 🛡️ **BFF seguro** — o browser nunca chama o backend diretamente

---

## Por que isso importa

Plataformas centralizadas de vagas têm incentivos que não são os seus. Elas controlam o algoritmo, o acesso, o formato.

Este projeto coloca o controle de volta nas suas mãos.

### Para desenvolvedores

Você pode montar seu próprio agregador de vagas em stack, nível ou localização que quiser — sem depender de plataformas que não foram feitas para isso. A stack é familiar: .NET 8, Postgres, Next.js, Docker.

### Para comunidades

Quer um bot de vagas no Telegram da sua comunidade de devs? Um feed curado de posições remotas em React? Um site de vagas focado em determinada stack? A infraestrutura está pronta. Você configura as fontes e distribui como quiser.

---

## 🗺️ Arquitetura

O fluxo é direto:

```
[Fontes externas]
       ↓
  Worker (.NET)
  fetch → parse → normalize → dedupe
       ↓                ↓
   PostgreSQL       Meilisearch
       ↑                ↑
     Jobs.Api (Minimal API)
       ↑
  Next.js BFF (Route Handlers)
       ↑
  Browser / Bot / Integração externa
```

Postgres é a fonte de verdade. Meilisearch é o motor de busca. O worker roda em background e alimenta os dois. A API expõe os dados. O frontend consome a API via BFF server-side — o browser nunca fala diretamente com o backend.

---

## 🚀 Como começar

Você precisa de: Docker, .NET 8 SDK e Node.js 20+.

```bash
# 1. Clone o repositório
git clone https://github.com/vitorvaf/job-search-engine.git
cd job-search-engine

# 2. Suba a infra (Postgres, Meilisearch, Redis)
cp .env.example .env
docker compose up -d

# 3. Rode a API
dotnet run --project src/backend/Jobs.Api
# → http://localhost:5004 | Swagger: http://localhost:5004/swagger

# 4. Rode o worker (ingestão única para testar)
dotnet run --project src/backend/Jobs.Worker -- --run-once --source=InfoJobs

# 5. Rode o frontend
cp src/frontend/.env.local.example src/frontend/.env.local
# O arquivo .env.local deve conter:
# BACKEND_URL=http://localhost:5004
cd src/frontend && npm install && npm run dev
# → http://localhost:3000
```

Pronto. Você tem um motor de busca de vagas rodando localmente.

---

## 🔌 Integração via API

A API expõe os endpoints principais:

| Endpoint | Descrição |
|----------|-----------|
| `GET /api/jobs` | Listagem com busca, filtros e paginação |
| `GET /api/jobs/{id}` | Detalhe da vaga |
| `GET /api/sources` | Fontes ativas |
| `POST /api/ingestion/jobs/bulk` | Ingestão externa (n8n, Firecrawl, etc.) |

Exemplo de busca:

```bash
curl "http://localhost:3000/api/jobs?q=react&workMode=Remote&tags=react,nextjs&sort=postedAt:desc"
```

O endpoint de ingestão bulk aceita até 100 itens por request, com idempotência e relatório de erros por item — pronto para pipelines externos.

---

## 💡 Casos de uso

- **Site de vagas personalizado** — filtre por stack, senioridade, modelo de trabalho
- **Bot de vagas no Telegram** — feed diário ou alertas por categoria
- **Monitor de oportunidades** — acompanhe vagas em .NET, React, DevOps sem ruído
- **Agregador para comunidades tech** — dê à sua comunidade um feed curado e limpo
- **Infraestrutura interna** — centralize vagas relevantes para um time de recrutamento

---

## 🛣️ Roadmap

| Status | Item |
|--------|------|
| ✅ | Ingestão multi-fonte (InfoJobs, Gupy, Workday, sites corporativos) |
| ✅ | Deduplicação por fingerprint |
| ✅ | API REST + Swagger |
| ✅ | Frontend com busca, filtros e paginação |
| ✅ | Endpoint de ingestão bulk (n8n, Firecrawl, automações) |
| 🔄 | Autenticação e favoritos |
| 📋 | Bot Telegram (digest diário + alertas) |
| 📋 | Ranking inteligente de vagas |
| 📋 | Recomendações com IA |
| 📋 | Dashboard de métricas por fonte |
| 📋 | Mais fontes de vagas |

---

## 🤝 Contribuindo

Contribuições são bem-vindas — especialmente novas fontes de vagas e melhorias no pipeline de normalização.

```bash
# Fork, clone e crie uma branch
git checkout -b feat/minha-feature

# Rode os testes antes de abrir o PR
dotnet test src/backend/Jobs.sln
cd src/frontend && npm run lint && npm run build
node scripts/check-boundary-drift.mjs
```

Veja [CONTRIBUTING.md](CONTRIBUTING.md) para o guia completo — convenções de commit, como adicionar uma nova fonte, e o que revisar antes de submeter.

Abra uma [issue](../../issues) para sugerir novas fontes, reportar bugs ou discutir melhorias. Discussões abertas em [Discussions](../../discussions).

---

## Configuração para assistentes de IA

Este repositório tem configuração pronta para **Claude Code** e **GitHub Copilot** — com instruções de contexto, slash commands e regras alinhadas ao código atual.

| Ferramenta | Documentação |
|------------|-------------|
| Claude Code | [`.claude/`](.claude/) — comandos `/new-source`, `/new-test`, `/trace-job-flow`, `/review-boundary` |
| GitHub Copilot | [`.github/`](.github/) — instruções por stack e prompts contextuais |

---

## 📄 Licença

[MIT](LICENSE) — use, modifique e distribua como quiser.

---

<div align="center">

Feito para a comunidade de tecnologia brasileira — e para qualquer dev que quer buscar vagas sem depender de plataformas fechadas.

**⭐ Se este projeto foi útil, considere dar uma estrela.**

</div>
