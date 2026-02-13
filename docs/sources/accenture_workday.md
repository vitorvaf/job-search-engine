# Accenture Workday (myworkdayjobs)

Objetivo: integrar Accenture via backend Workday (`myworkdayjobs`) e nao via parsing HTML de `accenture.com`.

## Como descobrir o endpoint real (XHR)

1. Abra a pagina de busca de vagas da Accenture no navegador.
2. Abra DevTools (`F12`) e va em `Network`.
3. Filtre por `Fetch/XHR`.
4. Recarregue a pagina e procure uma chamada que retorna JSON com `jobPostings`.
5. Normalmente a URL comeca com `/wday/cxs/...`.
6. Clique na request e use `Copy` -> `Copy as cURL`.
7. Redija cookies/tokens antes de salvar/compartilhar.

## Endpoint identificado (fixture desta base)

- Listagem: `POST /wday/cxs/accenture/AccentureCareers/jobs`
- Detalhe: `GET /wday/cxs/accenture/AccentureCareers/job/<...>`

Esses caminhos estao registrados nas fixtures:
- `src/backend/tests/fixtures/accenture_workday_jobs_page1.json`
- `src/backend/tests/fixtures/accenture_workday_job_detail.json`

## Exemplo (redigido)

```bash
curl 'https://accenture.wd103.myworkdayjobs.com/wday/cxs/accenture/AccentureCareers/jobs' \
  -X POST \
  -H 'accept: application/json' \
  -H 'content-type: application/json' \
  -H 'user-agent: JobSearchEngineBot/0.1 (contact: ...)' \
  --data-raw '{"appliedFacets":{},"limit":50,"offset":0,"searchText":""}'
```

Resposta (recorte):

```json
{
  "total": 1264,
  "jobPostings": [
    {
      "title": "Application Developer",
      "externalPath": "/job/Sao-Paulo/Application-Developer_R00123451",
      "locationsText": "Sao Paulo, Brazil",
      "id": "R00123451"
    }
  ]
}
```

## Observacoes

- A listagem em `accenture.com` depende de JavaScript; por isso a ingestao deve ir direto no Workday JSON.
- Se o host retornar `401/403/429`, tratar como `blocked/unauthorized` e seguir sem quebrar o run.
