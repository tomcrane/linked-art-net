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

-- version 2 (2026-10-18): the workbench's users, projects, memberships and notes (design doc 12, section 4)

create table if not exists users (
    login           text primary key,
    display_name    text,
    created_at      timestamptz not null default now(),
    last_seen       timestamptz
);

create table if not exists projects (
    dataset         text primary key,
    name            text not null,
    description     text,
    identifier_base text not null,
    manifest        jsonb,
    created_at      timestamptz not null default now(),
    updated_at      timestamptz not null default now()
);

create table if not exists memberships (
    dataset         text not null references projects(dataset) on delete cascade,
    login           text not null references users(login) on delete cascade,
    role            text not null,
    added_at        timestamptz not null default now(),
    primary key (dataset, login)
);

create table if not exists notes (
    id              bigserial primary key,
    dataset         text not null,
    target          text not null,
    author          text,
    text            text not null,
    created_at      timestamptz not null default now()
);
create index if not exists notes_target on notes (dataset, target);

insert into schema_version (version) values (2) on conflict do nothing;

-- version 3 (2026-10-18, after review): a string has at most one valid decision, enforced; each decision records
-- the one it superseded so an undo restores exactly that. Older duplicates, if any, keep only the newest valid.
alter table decisions add column if not exists supersedes bigint references decisions(id);
update decisions d set valid = false
  where d.valid and d.id < (select max(x.id) from decisions x where x.source_string_id = d.source_string_id and x.valid);
create unique index if not exists decisions_one_valid on decisions (source_string_id) where valid;

insert into schema_version (version) values (3) on conflict do nothing;
