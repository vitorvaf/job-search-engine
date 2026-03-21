# Support

## How to Get Help

### Documentation first

Before opening an issue, please check the existing documentation:

| Document | Content |
|----------|---------|
| [README.md](README.md) | Setup, running locally, API examples |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Development workflow, conventions, adding new sources |
| [docs/PROJECT_RULES.md](docs/PROJECT_RULES.md) | Team rules, definition of done |
| [docs/02_domain_model.md](docs/02_domain_model.md) | `JobPosting` canonical model |
| [docs/03_architecture.md](docs/03_architecture.md) | Architecture decisions |
| [docs/04_ingestion_sources.md](docs/04_ingestion_sources.md) | How connectors work |
| [docs/07_api_contracts.md](docs/07_api_contracts.md) | REST API contracts |

### Common issues

**Docker services not starting?**
```bash
# Check service health
docker compose ps
docker compose logs postgres
docker compose logs meilisearch
```

**`dotnet run` fails with connection error?**  
Check that `src/backend/.env` exists (copy from `.env.example`) and that PostgreSQL is healthy:
```bash
docker compose up -d
docker compose ps  # all services should show "healthy"
```

**Frontend shows no jobs?**  
Check that `src/frontend/.env.local` contains `BACKEND_URL=http://localhost:5000` and the API is running.

**Tests fail?**  
```bash
dotnet test src/backend/Jobs.sln --verbosity normal
```
Fixtures must be in `src/backend/tests/fixtures/` — ensure they were not deleted.

---

## Reporting Bugs

Use the [Bug Report issue template](../../issues/new?template=bug_report.yml).

## Requesting Features

Use the [Feature Request issue template](../../issues/new?template=feature_request.yml).

## Proposing a New Job Source

Use the [New Source issue template](../../issues/new?template=new_source.yml).

## Security Vulnerabilities

See [SECURITY.md](SECURITY.md) — **do not open a public issue for security reports**.

---

## Community

For general questions or discussions, open a [GitHub Discussion](../../discussions).
