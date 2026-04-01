---
paths:
  - "src/frontend/**/*.{ts,tsx,js,jsx,mjs,cjs,json}"
---

# Frontend rules (Next.js / TypeScript)

- Use the Next.js App Router only; do not add a `pages/` directory.
- The browser never calls `Jobs.Api` directly. Browser traffic goes through Route Handlers in `src/frontend/app/api/`.
- Prefer React Server Components for data-fetching pages; add `"use client"` only when browser APIs or interactive hooks are required.
- Keep backend fetch plumbing in `src/frontend/lib/api-proxy.ts` and normalize responses in `src/frontend/lib/normalizers.ts`.
- Validate and sanitize query params in Route Handlers before forwarding them to the backend.
- Keep `BACKEND_URL` and other backend connection details server-side only.
- The raw backend contract is defined by `src/backend/Jobs.Api/Program.cs` and documented in `docs/07_api_contracts.md`.
- `src/frontend/lib/types.ts` contains the normalized BFF/frontend models used by the UI, not a byte-for-byte mirror of the raw backend payload.
- If filters, enums, sorting, or pagination change, review `src/backend/Jobs.Api/Program.cs`, `src/frontend/app/api/`, `src/frontend/lib/api-proxy.ts`, `src/frontend/lib/normalizers.ts`, `src/frontend/lib/types.ts`, `src/frontend/lib/constants.ts`, and the relevant components together.
- Prefer Tailwind utility classes and the existing visual patterns in the repo.
- Use `lucide-react` for icons when icons are needed.
