---
applyTo: "src/frontend/**"
---

# Frontend Coding Instructions (Next.js 15 / TypeScript)

These instructions apply to all files under `src/frontend/`. Follow them strictly when generating or modifying TypeScript/React code.

## Project Layout

```
app/              Pages (RSC by default) and Route Handlers (BFF)
  api/            BFF Route Handlers — the ONLY bridge between browser and backend
  vagas/[id]/     Job detail page (SSR)
  favoritos/      Favourites page (client-rendered, localStorage)
components/       React components
lib/
  api-proxy.ts    Internal fetch helper used by Route Handlers
  api.ts          Typed wrappers called from RSC pages
  types.ts        TypeScript interfaces mirroring the API contract
  storage.ts      localStorage helpers for favourites
  normalizers.ts  Data transformation utilities
  constants.ts    Shared constants
  utils.ts        Generic utilities
```

## Framework Rules

- Use **Next.js App Router** exclusively. Never create or suggest a `pages/` directory.
- Default to **React Server Components** (RSC) for pages and data-fetching components.
- Add `"use client"` **only** when the component needs browser APIs (e.g., `localStorage`, `window`, event handlers, `useState`, `useEffect`).
- Route Handlers live in `app/api/` and implement the BFF (Backend-For-Frontend) pattern.

## BFF Rule — Critical

> The browser **never** calls `Jobs.Api` directly.

All data flows through Route Handlers in `src/frontend/app/api/`:
- Validate and sanitise query params in Route Handlers before forwarding to the backend.
- Use `src/frontend/lib/api-proxy.ts` for the actual backend HTTP call inside Route Handlers.
- Client components call the Next.js Route Handlers (`/api/jobs`, `/api/jobs/[id]`, `/api/sources`) — never `http://localhost:5000` or `BACKEND_URL` directly.
- The `BACKEND_URL` env var must only be read server-side (Route Handlers and RSC).

## TypeScript

- All interfaces and types live in `src/frontend/lib/types.ts`.
- Mirror the backend REST contract exactly — do NOT invent fields not in `docs/07_api_contracts.md`.
- Use `interface` for object shapes, `type` for unions/intersections.
- Prefer explicit return types on functions exported from `lib/`.
- Never use `any`. Use `unknown` and narrow the type where necessary.

## Styling

- **Tailwind CSS utility classes only** — no inline `style` props, no CSS Modules, no CSS-in-JS.
- Use the existing `tailwind.config.ts` — do not add new plugins or theme tokens without a clear need.
- Follow the established class ordering: layout → spacing → sizing → typography → colors → effects.

## Icons

- Use **Lucide React** (`lucide-react`) exclusively.
- Never add other icon libraries (Heroicons, FontAwesome, etc.).
- Import icons individually: `import { Search } from "lucide-react"`.

## State & Storage

- Favourites persist in `localStorage` via `src/frontend/lib/storage.ts` — never hit the backend for favourites.
- Do NOT add client-side state management libraries (Redux, Zustand, Jotai, etc.) — use React state and Server Actions for simple cases.

## Data Fetching

- Use `fetch` with `cache` options in RSC pages for data fetching (Next.js 15 built-in caching).
- Pass `revalidate` or `no-store` explicitly — do not rely on defaults.

## Component Rules

- Functional components only — no class components.
- File naming: `kebab-case.tsx` (e.g., `job-card.tsx`), matching the export name in PascalCase (`JobCard`).
- One component per file.
- Props typed with an inline `interface` inside the same file.

## Forbidden Patterns

- Do NOT import from the backend project in the frontend — the contract is the HTTP API only.
- Do NOT create a `pages/` directory.
- Do NOT add inline `style` attributes.
- Do NOT add additional icon libraries.
- Do NOT read `BACKEND_URL` in client components.
- Do NOT call the backend API from React client components — always via the BFF Route Handlers.
