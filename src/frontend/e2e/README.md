# E2E Scenarios

This folder contains Playwright end-to-end scenarios for the frontend.

## Structure

- `scenarios/`: end-to-end test files grouped by domain.
- `utils/`: shared helpers used by scenarios (Mailpit polling, account setup, etc).

## Current scenarios

- `scenarios/auth/login-name-logout.spec.ts`
  - Creates a local account via BFF (`/api/account/register`)
  - Verifies email by reading Mailpit message
  - Performs login via `/entrar`
  - Asserts user name appears in header
  - Asserts logout (`Sair`) returns to guest header (`Entrar`)

- `scenarios/auth/signup-verify-login.spec.ts`
  - Performs signup through `/cadastro`
  - Reads verification token from Mailpit inbox
  - Verifies email through `/verificar-email`
  - Logs in via `/entrar`
  - Asserts authenticated header (user name + `Sair`)

## Prerequisites

1. Stack running (`frontend`, `api`, `postgres`, `mailpit`):

```bash
docker compose up -d
```

2. Schema applied with identity tables (if needed):

```bash
docker compose exec -T postgres psql -U jobs -d jobs < src/backend/Jobs.Infrastructure/Data/schema.sql
```

3. Playwright browser installed:

```bash
npm run e2e:install
```

## Run

From `src/frontend`:

```bash
npm run test:e2e
```

Only auth scenario:

```bash
npm run test:e2e:auth
```

Only onboarding scenario:

```bash
npm run test:e2e:onboarding
```

Optional envs:

- `E2E_BASE_URL` (default: `http://localhost:3000`)
- `E2E_MAILPIT_URL` (default: `http://localhost:8025`)
