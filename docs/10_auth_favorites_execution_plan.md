# Plano de Execucao: Auth, Conta e Favoritos

## Objetivo

Entregar no MVP:
- cadastro local com `email + senha`
- login local com `Credentials`
- login social com `GitHub` e `Google`
- verificacao obrigatoria de email
- recuperacao de senha
- painel `/favoritos` protegido
- favoritos persistidos por usuario

Fica para fase 2:
- login com `LinkedIn`
- linking manual de provedores
- revogacao de sessoes
- troca de email

## Decisoes Fechadas

- A autenticacao do usuario termina no `Next.js` via BFF.
- O browser continua falando apenas com `src/frontend/app/api/`.
- O `Jobs.Api` continua como `Minimal API` em `Program.cs`.
- Dados de conta, tokens e favoritos ficam no Postgres.
- Em desenvolvimento, o envio de emails usa `Mailpit`.
- `GitHub` e `Google` entram no mesmo MVP do cadastro local.
- `email_verified_at` e obrigatorio para login local.
- Reset de senha entra no mesmo MVP.

## Ordem de Execucao

1. Configuracao e infraestrutura de email/auth
2. Modelo de dados de identidade e favoritos
3. Servicos backend de identidade
4. Endpoints backend de conta e favoritos
5. Sessao no frontend com `Auth.js`
6. Paginas de conta e fluxos de usuario
7. Protecao da rota `/favoritos`
8. Migracao dos favoritos para persistencia por usuario
9. Importacao opcional do legado em `localStorage`
10. Hardening, docs e validacao final

## Fase 1: Configuracao e Infra

### Backend

- Estender `src/backend/Jobs.Infrastructure/Options/AppOptions.cs` com secoes para:
  - `Auth`
  - `Email`
  - `PublicUrls`
- Adicionar `BffInternalApiKey` para chamadas internas BFF -> API.
- Adicionar TTLs configuraveis para:
  - `EmailVerification`
  - `PasswordReset`
- Definir remetente padrao de email.

### Frontend

- Adicionar configuracao para `Auth.js`.
- Definir envs para `GitHub`, `Google`, `AUTH_SECRET`, `NEXT_PUBLIC_SITE_URL` e `BACKEND_URL`.

### Docker / Dev

- Atualizar `docker-compose.yaml` com servico `mailpit`.
- Expor:
  - porta SMTP do `Mailpit`
  - porta da UI web do `Mailpit`
- Configurar o backend para usar SMTP local em desenvolvimento.

### Checklist

- [ ] Definir formato final das options de auth/email
- [ ] Adicionar `Mailpit` no compose
- [ ] Atualizar `.env.example`, `src/backend/.env.example` e `src/frontend/.env.local.example`
- [ ] Definir chave interna BFF -> API

## Fase 2: Modelo de Dados

### Arquivos

- `src/backend/Jobs.Infrastructure/Data/schema.sql`
- `src/backend/Jobs.Infrastructure/Data/JobsDbContext.cs`
- `src/backend/Jobs.Infrastructure/Data/Entities/*`

### Tabelas

#### `users`
- `id`
- `email`
- `normalized_email`
- `display_name`
- `avatar_url`
- `email_verified_at`
- `created_at`
- `last_login_at`
- `status`

#### `user_credentials`
- `user_id`
- `password_hash`
- `created_at`
- `updated_at`

#### `user_identities`
- `user_id`
- `provider`
- `provider_user_id`
- `provider_email`
- `created_at`

#### `user_action_tokens`
- `id`
- `user_id`
- `type`
- `token_hash`
- `expires_at`
- `consumed_at`
- `created_at`

#### `favorite_jobs`
- `user_id`
- `job_posting_id`
- `created_at`

### Restricoes

- `unique(users.normalized_email)`
- `unique(user_credentials.user_id)`
- `unique(user_identities.provider, user_identities.provider_user_id)`
- `unique(favorite_jobs.user_id, favorite_jobs.job_posting_id)`

### Checklist

- [ ] Criar tabelas novas no `schema.sql`
- [ ] Criar entidades C# correspondentes
- [ ] Registrar novos `DbSet`s no `JobsDbContext`
- [ ] Adicionar indices e restricoes necessarias

## Fase 3: Servicos Backend

### Arquivos principais

- `src/backend/Jobs.Infrastructure/DependencyInjection.cs`
- novos servicos sob `src/backend/Jobs.Infrastructure/`

### Servicos necessarios

- `IPasswordHasher` ou adaptacao do `PasswordHasher<TUser>`
- servico de geracao de token seguro
- servico de hash e consumo de token
- `IEmailSender`
- implementacao SMTP para dev/producao
- servico de cadastro local
- servico de verificacao de email
- servico de reset de senha
- servico de resolucao de login social
- servico de favoritos

### Regras

- Senha nunca e armazenada em texto puro.
- Token nunca e armazenado em texto puro.
- Novo token de verificacao invalida os anteriores do mesmo tipo.
- Novo token de reset invalida os anteriores do mesmo tipo.
- Login local so e aceito quando `email_verified_at` estiver preenchido.
- Login social so pode entrar quando o email puder ser tratado como verificado.

### Checklist

- [ ] Implementar hash de senha
- [ ] Implementar emissao e consumo de token com expiracao
- [ ] Implementar envio de email via `IEmailSender`
- [ ] Implementar regra de cadastro local
- [ ] Implementar regra de verificacao de email
- [ ] Implementar regra de forgot/reset password
- [ ] Implementar regra de resolucao para `GitHub` e `Google`

## Fase 4: Endpoints Backend

### Arquivo principal

- `src/backend/Jobs.Api/Program.cs`

### Endpoints de conta

- `POST /api/account/register`
- `POST /api/account/verify-email`
- `POST /api/account/resend-verification`
- `POST /api/account/password/forgot`
- `POST /api/account/password/reset`
- `POST /api/account/auth/credentials`
- `POST /api/account/auth/oauth`

### Endpoints de favoritos

- `GET /api/me/favorites`
- `PUT /api/me/favorites/{jobId}`
- `DELETE /api/me/favorites/{jobId}`

### Regras de comportamento

- `register` cria usuario nao verificado e dispara email de verificacao.
- `verify-email` consome token de uso unico.
- `resend-verification` gera novo token e invalida o anterior.
- `forgot password` responde de forma neutra, sem revelar existencia do email.
- `password reset` consome token de uso unico e atualiza a senha.
- `auth/credentials` valida senha e email verificado.
- `auth/oauth` cria ou reutiliza usuario interno para `GitHub`/`Google`.
- Favoritos devem ser idempotentes.

### Checklist

- [ ] Definir DTOs de request/response
- [ ] Implementar endpoints de conta no `Program.cs`
- [ ] Implementar endpoints de favoritos no `Program.cs`
- [ ] Proteger endpoints internos de favoritos para uso via BFF
- [ ] Garantir `no-store` em respostas personalizadas

## Fase 5: Sessao no Frontend

### Arquivos

- `src/frontend/package.json`
- `src/frontend/app/api/auth/[...nextauth]/route.ts`
- `src/frontend/auth.ts`

### Providers

- `Credentials`
- `GitHub`
- `Google`

### Fluxo

- `Credentials` chama `POST /api/account/auth/credentials`.
- `GitHub` e `Google` fazem callback no frontend e depois chamam `POST /api/account/auth/oauth`.
- A sessao armazenada pelo frontend precisa conter o `user_id` interno.

### Checklist

- [ ] Adicionar dependencias de auth no frontend
- [ ] Configurar `Credentials Provider`
- [ ] Configurar `GitHub Provider`
- [ ] Configurar `Google Provider`
- [ ] Persistir `user_id` interno na sessao
- [ ] Manter redirect de retorno apos login

## Fase 6: Paginas de Conta

### Arquivos previstos

- `src/frontend/app/entrar/page.tsx`
- `src/frontend/app/cadastro/page.tsx`
- `src/frontend/app/verificar-email/page.tsx`
- `src/frontend/app/esqueci-senha/page.tsx`
- `src/frontend/app/redefinir-senha/page.tsx`
- `src/frontend/components/header.tsx`

### Fluxos

#### `/cadastro`
- campos: nome, email, senha
- envia cadastro via BFF
- exibe instrucoes para confirmar email

#### `/entrar`
- login com email/senha
- botoes de `GitHub` e `Google`
- link para `esqueci-senha`

#### `/verificar-email`
- recebe token pela URL
- chama BFF para confirmar a conta

#### `/esqueci-senha`
- recebe email
- sempre exibe mensagem neutra de sucesso

#### `/redefinir-senha`
- recebe token pela URL
- permite definir nova senha

### Checklist

- [ ] Criar pagina de cadastro
- [ ] Criar pagina de login
- [ ] Criar pagina de verificacao
- [ ] Criar pagina de forgot password
- [ ] Criar pagina de reset password
- [ ] Atualizar `header` com estado logado/deslogado

## Fase 7: Protecao de Rota

### Arquivos

- `src/frontend/app/favoritos/page.tsx`
- `src/frontend/middleware.ts` opcional

### Regras

- `/favoritos` exige sessao valida.
- Usuario deslogado deve ser redirecionado para `/entrar`.
- Usuario logado acessa o painel normalmente.

### Checklist

- [ ] Proteger `/favoritos` no server-side do App Router
- [ ] Adicionar redirect com retorno para a rota original
- [ ] Revisar se `middleware.ts` vale a pena ou se a verificacao na pagina basta

## Fase 8: Favoritos Persistidos

### Backend

- `GET /api/me/favorites`
- `PUT /api/me/favorites/{jobId}`
- `DELETE /api/me/favorites/{jobId}`

### BFF

- `src/frontend/app/api/favorites/route.ts`
- `src/frontend/app/api/favorites/[id]/route.ts`
- `src/frontend/lib/api-proxy.ts`
- `src/frontend/lib/types.ts`
- `src/frontend/lib/normalizers.ts`

### UI

- `src/frontend/components/favorite-button.tsx`
- `src/frontend/components/favorites-list.tsx`
- `src/frontend/app/favoritos/page.tsx`

### Regras

- Deslogado pode receber CTA para login ao tentar favoritar.
- Logado salva/remove via API, nao via `localStorage`.
- Lista de favoritos deve vir do backend.
- Endpoints de favoritos devem usar `cache: "no-store"`.

### Checklist

- [ ] Criar rotas BFF de favoritos
- [ ] Adicionar tipos e normalizadores do novo contrato
- [ ] Migrar `FavoriteButton` para API
- [ ] Migrar `FavoritesList` para API
- [ ] Proteger a pagina `/favoritos`

## Fase 9: Importacao do Legado

### Arquivos

- `src/frontend/lib/storage.ts`

### Estrategia

- Detectar favoritos antigos do navegador apos login.
- Oferecer importacao unica para a conta.
- Deduplicar por `jobId`.

### Checklist

- [ ] Decidir se a importacao entra no MVP inicial ou no fechamento do MVP
- [ ] Implementar fluxo de importacao opcional
- [ ] Garantir deduplicacao no backend

## Fase 10: Hardening e Validacao

### Seguranca

- Rate limiting em:
  - cadastro
  - login local
  - resend verification
  - forgot password
  - reset password
- Tokens com uso unico e expiracao curta.
- Respostas neutras para emails inexistentes.
- Sem cache compartilhado para rotas personalizadas.

### Testes e validacoes

- `dotnet test src/backend/Jobs.sln`
- `node scripts/check-boundary-drift.mjs`
- em `src/frontend`: `npm run lint`
- em `src/frontend`: `npm run build`

### Casos manuais minimos

- cadastro local
- recebimento de email no `Mailpit`
- bloqueio de login antes da verificacao
- verificacao de email por link
- login local com email verificado
- login com `GitHub`
- login com `Google`
- forgot password
- reset password
- acesso protegido a `/favoritos`
- favoritar e desfavoritar

### Checklist

- [ ] Adicionar rate limiting
- [ ] Revisar expiracao e invalidacao de tokens
- [ ] Atualizar docs e env examples
- [ ] Validar fluxo completo com `Mailpit`

## Contratos e Regras de Negocio

### Verificacao de email

- Obrigatoria para login local.
- `Google` so deve entrar como verificado quando o provider devolver email verificado.
- `GitHub` so deve entrar quando houver email primario verificavel.

### Vinculacao de contas

- Para o MVP, a regra deve ser conservadora.
- So auto-vincular conta social a conta existente quando houver email verificado e coincidencia exata com `normalized_email`.
- Se nao houver email utilizavel, negar entrada com mensagem clara.

### Password reset

- Token de uso unico.
- Token com expiracao curta.
- Troca de senha invalida tokens antigos do mesmo tipo.
- Depois do reset, usuario deve autenticar novamente.

## Variaveis de Ambiente

### Frontend

- `AUTH_SECRET`
- `AUTH_GITHUB_ID`
- `AUTH_GITHUB_SECRET`
- `AUTH_GOOGLE_ID`
- `AUTH_GOOGLE_SECRET`
- `BACKEND_URL`
- `NEXT_PUBLIC_SITE_URL`

### Backend

- `ConnectionStrings__JobsDb`
- `Meilisearch__BaseUrl`
- `Meilisearch__MasterKey`
- envs de SMTP / `Mailpit`
- chave interna BFF -> API
- TTL de `EmailVerification`
- TTL de `PasswordReset`
- URL publica do frontend para links de email

## Sprints Sugeridas

### Sprint 1

- configuracao e envs
- `Mailpit`
- schema e entidades
- servicos de senha/token/email

### Sprint 2

- endpoints backend de conta
- `Credentials Provider`
- cadastro
- verificacao de email
- forgot/reset password

### Sprint 3

- `GitHub`
- `Google`
- endpoints backend de favoritos
- BFF de favoritos
- protecao de `/favoritos`
- UI de favoritos persistidos

### Sprint 4

- importacao opcional do legado
- hardening
- docs finais
- validacao ponta a ponta

## Arquivos Mais Impactados

### Frontend

- `src/frontend/package.json`
- `src/frontend/app/api/auth/[...nextauth]/route.ts`
- `src/frontend/auth.ts`
- `src/frontend/app/entrar/page.tsx`
- `src/frontend/app/cadastro/page.tsx`
- `src/frontend/app/verificar-email/page.tsx`
- `src/frontend/app/esqueci-senha/page.tsx`
- `src/frontend/app/redefinir-senha/page.tsx`
- `src/frontend/app/favoritos/page.tsx`
- `src/frontend/app/api/favorites/route.ts`
- `src/frontend/app/api/favorites/[id]/route.ts`
- `src/frontend/components/header.tsx`
- `src/frontend/components/favorite-button.tsx`
- `src/frontend/components/favorites-list.tsx`
- `src/frontend/lib/api-proxy.ts`
- `src/frontend/lib/types.ts`
- `src/frontend/lib/normalizers.ts`
- `src/frontend/lib/storage.ts`

### Backend

- `src/backend/Jobs.Api/Program.cs`
- `src/backend/Jobs.Infrastructure/DependencyInjection.cs`
- `src/backend/Jobs.Infrastructure/Options/AppOptions.cs`
- `src/backend/Jobs.Infrastructure/Data/schema.sql`
- `src/backend/Jobs.Infrastructure/Data/JobsDbContext.cs`
- `src/backend/Jobs.Infrastructure/Data/Entities/*`

### Infra / Docs

- `docker-compose.yaml`
- `.env.example`
- `src/backend/.env.example`
- `src/frontend/.env.local.example`
- `README.md`
- `docs/07_api_contracts.md`
