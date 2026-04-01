---
applyTo: "src/frontend/**"
---

# Frontend Coding Instructions (Next.js / TypeScript)

## Structure

```text
app/                 App Router pages and route handlers
  api/               BFF route handlers; the only bridge between browser and backend
components/          UI components
lib/
  api-proxy.ts       Query sanitization and backend fetch plumbing
  normalizers.ts     Raw backend payload -> normalized frontend model
  types.ts           Raw API shapes + normalized frontend models
  constants.ts       Filter options and defaults
```

## Rules

- Use the App Router only. Do not create a `pages/` directory.
- The browser never calls `Jobs.Api` directly.
- Route handlers in `app/api/` implement the BFF boundary.
- Keep backend fetch plumbing in `lib/api-proxy.ts`.
- Keep payload adaptation in `lib/normalizers.ts`.
- `lib/types.ts` contains the models consumed by the UI; it is not a byte-for-byte copy of the raw backend payload.
- `BACKEND_URL` must remain server-side only.
- Do not import backend assemblies or C# contracts into the frontend.

## Boundary review triggers

If filters, sort, pagination, or enum values change, review:
- `src/backend/Jobs.Api/Program.cs`
- `src/frontend/app/api/`
- `src/frontend/lib/api-proxy.ts`
- `src/frontend/lib/normalizers.ts`
- `src/frontend/lib/types.ts`
- `src/frontend/lib/constants.ts`
- the affected UI components
