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
    origin_url varchar(1024),
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

CREATE UNIQUE INDEX IF NOT EXISTS ux_job_postings_origin_url
    ON job_postings (origin_url)
    WHERE origin_url IS NOT NULL AND origin_url <> '';

CREATE TABLE IF NOT EXISTS users (
    id uuid PRIMARY KEY,
    email varchar(320) NOT NULL,
    normalized_email varchar(320) NOT NULL,
    display_name varchar(160) NOT NULL DEFAULT '',
    avatar_url varchar(1024),
    email_verified_at timestamptz,
    status varchar(32) NOT NULL DEFAULT 'Active',
    created_at timestamptz NOT NULL DEFAULT now(),
    last_login_at timestamptz
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_users_normalized_email
    ON users (normalized_email);

CREATE TABLE IF NOT EXISTS user_credentials (
    user_id uuid PRIMARY KEY REFERENCES users (id) ON DELETE CASCADE,
    password_hash varchar(512) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS user_identities (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    provider varchar(40) NOT NULL,
    provider_user_id varchar(255) NOT NULL,
    provider_email varchar(320),
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_user_identities_provider_subject
    ON user_identities (provider, provider_user_id);

CREATE INDEX IF NOT EXISTS ix_user_identities_user_id
    ON user_identities (user_id);

CREATE TABLE IF NOT EXISTS user_action_tokens (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    type varchar(40) NOT NULL,
    token_hash varchar(255) NOT NULL,
    expires_at timestamptz NOT NULL,
    consumed_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_user_action_tokens_type_hash
    ON user_action_tokens (type, token_hash);

CREATE INDEX IF NOT EXISTS ix_user_action_tokens_user_type
    ON user_action_tokens (user_id, type);

CREATE INDEX IF NOT EXISTS ix_user_action_tokens_expires_at
    ON user_action_tokens (expires_at);

CREATE TABLE IF NOT EXISTS favorite_jobs (
    user_id uuid NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    job_posting_id uuid NOT NULL REFERENCES job_postings (id) ON DELETE CASCADE,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, job_posting_id)
);

CREATE INDEX IF NOT EXISTS ix_favorite_jobs_job_posting_id
    ON favorite_jobs (job_posting_id);
