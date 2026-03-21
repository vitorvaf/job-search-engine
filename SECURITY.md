# Security Policy

## Supported Versions

This project is in active development (MVP phase). Security fixes are applied to the latest version on the `main` branch only.

| Version / Branch | Supported |
|-----------------|-----------|
| `main` (latest) | ✅ |
| Older branches  | ❌ |

---

## Reporting a Vulnerability

**Please do not open a public GitHub Issue for security vulnerabilities.**

To report a vulnerability, use one of the following channels:

1. **GitHub Private Vulnerability Reporting** (preferred):  
   On the repository page, go to **Security → Report a vulnerability** and fill in the form.  
   This keeps the report private until a fix is released.

2. **Email** (fallback):  
   Send a detailed report to the repository owner via the email listed on their [GitHub profile](https://github.com/vitorvaf).

### What to include in your report

- **Description**: A clear description of the vulnerability and its potential impact.
- **Steps to reproduce**: Minimal steps or a proof-of-concept to validate the issue.
- **Affected component**: Which part of the system is affected (API, Worker, frontend, Docker config, etc.).
- **Suggested fix**: If you have a proposed fix or mitigation, please include it.

---

## What to Expect

| Timeframe | Action |
|-----------|--------|
| 48 hours | Acknowledgement of receipt |
| 7 days | Initial assessment and severity classification |
| 30 days | Fix or mitigation released (for confirmed vulnerabilities) |
| After fix | Coordinated disclosure — CVE filed if applicable |

---

## Scope

### In scope

- Injection vulnerabilities in the API route handlers (SQL injection, filter injection in Meilisearch queries)
- Authentication/authorisation bypass (if auth is added in future)
- Server-Side Request Forgery (SSRF) in ingestion connectors
- Sensitive data exposure via API responses (PII, credentials)
- Insecure direct object references in `GET /api/jobs/{id}`
- Secrets committed to source code

### Out of scope

- Vulnerabilities in third-party services (PostgreSQL, Meilisearch, Redis)
- Rate limiting abuse on job board source websites (scraping-related)
- Denial-of-service via large payloads (no auth model in MVP)
- Issues in browsers/OS used by end users

---

## Security Best Practices for Contributors

- Never commit secrets to the repository — use environment variables or `.env` files (see `.env.example`).
- The `BACKEND_URL` env var must only be read server-side (Next.js Route Handlers / RSC) — never exposed to the browser.
- All filter values injected into Meilisearch queries must go through the `EscapeFilterValue` helper in `Jobs.Api/Program.cs`.
- New `IJobSource` connectors must not follow arbitrary redirects or make requests to URLs constructed from untrusted user input.
