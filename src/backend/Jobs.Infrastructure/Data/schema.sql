CREATE TABLE IF NOT EXISTS sources (
    id uuid PRIMARY KEY,
    name varchar(120) NOT NULL,
    type integer NOT NULL,
    base_url varchar(1024),
    enabled boolean NOT NULL DEFAULT true,
    rate_limit_policy_json jsonb NOT NULL DEFAULT '{}'::jsonb
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_sources_name_type
    ON sources (name, type);

CREATE TABLE IF NOT EXISTS ingestion_runs (
    id uuid PRIMARY KEY,
    source_id uuid NOT NULL REFERENCES sources (id),
    started_at timestamptz NOT NULL,
    finished_at timestamptz,
    status varchar(40) NOT NULL,
    fetched integer NOT NULL DEFAULT 0,
    parsed integer NOT NULL DEFAULT 0,
    normalized integer NOT NULL DEFAULT 0,
    indexed integer NOT NULL DEFAULT 0,
    duplicates integer NOT NULL DEFAULT 0,
    errors integer NOT NULL DEFAULT 0,
    error_sample varchar(4000)
);

CREATE INDEX IF NOT EXISTS ix_ingestion_runs_source_id
    ON ingestion_runs (source_id);

CREATE TABLE IF NOT EXISTS job_postings (
    id uuid PRIMARY KEY,
    source_name varchar(120) NOT NULL,
    source_type integer NOT NULL,
    source_url varchar(1024) NOT NULL,
    source_job_id varchar(255),
    title varchar(400) NOT NULL,
    company_name varchar(240) NOT NULL,
    company_website varchar(1024),
    company_industry varchar(120),
    location_text varchar(240) NOT NULL DEFAULT '',
    country varchar(8),
    state varchar(80),
    city varchar(120),
    work_mode integer NOT NULL,
    seniority integer NOT NULL,
    employment_type integer NOT NULL,
    salary_min numeric,
    salary_max numeric,
    salary_currency varchar(10),
    salary_period varchar(32),
    description_text text NOT NULL DEFAULT '',
    tags text[] NOT NULL DEFAULT '{}',
    languages text[] NOT NULL DEFAULT '{}',
    posted_at timestamptz,
    captured_at timestamptz NOT NULL,
    last_seen_at timestamptz NOT NULL,
    status integer NOT NULL,
    fingerprint varchar(80) NOT NULL,
    cluster_id varchar(80),
    metadata_json jsonb NOT NULL DEFAULT '{}'::jsonb
);

CREATE INDEX IF NOT EXISTS ix_job_postings_fingerprint
    ON job_postings (fingerprint);

CREATE UNIQUE INDEX IF NOT EXISTS ux_job_postings_source_name_job_id
    ON job_postings (source_name, source_job_id)
    WHERE source_job_id IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_job_postings_source_url
    ON job_postings (source_url)
    WHERE source_url IS NOT NULL AND source_url <> '';
