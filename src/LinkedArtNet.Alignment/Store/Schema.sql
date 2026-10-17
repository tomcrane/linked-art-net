-- LinkedArtNet.Alignment store, PostgreSQL. Applied by PostgresAlignmentStore.InitialiseAsync; every statement is
-- idempotent so re-applying is safe. Later versions append statements guarded by schema_version.

create table if not exists schema_version (
    version     integer primary key,
    applied_at  timestamptz not null default now()
);

create table if not exists entities (
    id                  text primary key,
    dataset             text not null,
    class               text not null,
    label               text,
    labels              text[] not null default '{}',
    normalised_labels   text[] not null default '{}',
    begin_earliest      timestamptz,
    begin_latest        timestamptz,
    end_earliest        timestamptz,
    end_latest          timestamptz,
    lifespan_text       text,
    nationalities       text[] not null default '{}',
    roles               text[] not null default '{}',
    status              text not null default 'provisional',
    merged_into         text references entities(id),
    created_at          timestamptz not null default now(),
    updated_at          timestamptz not null default now(),
    run_id              text
);
create index if not exists entities_dataset_class on entities (dataset, class);
create index if not exists entities_normalised_labels on entities using gin (normalised_labels);
create index if not exists entities_status on entities (status);

create table if not exists entity_equivalents (
    entity_id       text not null references entities(id) on delete cascade,
    uri             text not null,
    source          text not null,
    method          text not null,
    confidence      double precision not null default 1,
    decided_at      timestamptz not null default now(),
    dataset_version text,
    run_id          text,
    primary key (entity_id, uri)
);
create index if not exists entity_equivalents_uri on entity_equivalents (uri);

create table if not exists source_strings (
    id              bigserial primary key,
    dataset         text not null,
    context         text not null,
    raw             text not null,
    normalised      text not null,
    parsed          jsonb,
    first_seen      timestamptz not null default now(),
    last_seen       timestamptz not null default now(),
    occurrences     integer not null default 1,
    unique (dataset, context, normalised)
);
create index if not exists source_strings_normalised on source_strings (dataset, normalised);

create table if not exists decisions (
    id                  bigserial primary key,
    source_string_id    bigint not null references source_strings(id) on delete cascade,
    entity_id           text references entities(id),
    method              text not null,
    evidence            jsonb,
    reviewer            text,
    decided_at          timestamptz not null default now(),
    dataset_version     text,
    run_id              text,
    valid               boolean not null default true
);
create index if not exists decisions_valid_string on decisions (source_string_id) where valid;
create index if not exists decisions_entity on decisions (entity_id);

create table if not exists authority_records (
    uri                 text primary key,
    source              text not null,
    label               text,
    labels              text[] not null default '{}',
    normalised_labels   text[] not null default '{}',
    type                text,
    begin_earliest      timestamptz,
    begin_latest        timestamptz,
    end_earliest        timestamptz,
    end_latest          timestamptz,
    lifespan_text       text,
    same_as             text[] not null default '{}',
    data                jsonb,
    fetched_at          timestamptz not null default now(),
    source_version      text
);
create index if not exists authority_records_source_labels on authority_records using gin (normalised_labels);
create index if not exists authority_records_same_as on authority_records using gin (same_as);

create table if not exists runs (
    id              text primary key,
    dataset         text not null,
    manifest        jsonb,
    dumps           jsonb,
    started_at      timestamptz not null default now(),
    finished_at     timestamptz,
    git_sha         text,
    counts          jsonb,
    report          text
);
create index if not exists runs_dataset_started on runs (dataset, started_at desc);

create table if not exists overrides (
    dataset         text not null,
    context         text not null default '',
    normalised      text not null,
    target          text not null,
    source          text not null,
    note            text,
    primary key (dataset, context, normalised)
);

insert into schema_version (version) values (1) on conflict do nothing;
