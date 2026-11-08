#!/usr/bin/env bash
# Read-only SQL against the alignment store, for the assistant's questions (the owner, 2026-11-08: "it's OK for you to
# query Postgres directly, and run tools, as long as you tell me what you are doing").
#
#   tools/pg.sh "select count(*) from entities"        one statement
#   tools/pg.sh -f query.sql                           a file
#   LINKEDART_PG_VAR=LINKEDART_TEST_PG tools/pg.sh ... another store (the test one)
#
# The connection string comes from the environment only: LINKEDART_PG in this shell, or the user-level variable of the
# same name when the shell was started before it was set (read through PowerShell; nothing is typed or stored here).
# Npgsql's form (Host=...;Port=...;Database=...;Username=...;Password=...) is turned into psql's. The session is
# read-only (default_transaction_read_only), so a slip cannot write: every write to the store goes through the Align
# tool's own commands, said first, as before.
set -euo pipefail
var="${LINKEDART_PG_VAR:-LINKEDART_PG}"
conn="${!var:-}"
if [ -z "$conn" ]; then
  conn=$(powershell -NoProfile -Command "[Environment]::GetEnvironmentVariable('$var','User')" 2>/dev/null | tr -d '\r')
fi
if [ -z "$conn" ]; then echo "tools/pg.sh: $var is not set in this shell or at user level (setx $var \$env:$var in PowerShell)" >&2; exit 2; fi

# Npgsql keys to psql conninfo keys; unknown keys are dropped
conninfo=""
IFS=';' read -ra pairs <<< "$conn"
for pair in "${pairs[@]}"; do
  key="${pair%%=*}"; value="${pair#*=}"
  key=$(echo "$key" | tr '[:upper:]' '[:lower:]' | tr -d ' ')
  case "$key" in
    host|server) conninfo+=" host=$value" ;;
    port) conninfo+=" port=$value" ;;
    database|dbname) conninfo+=" dbname=$value" ;;
    username|userid|user|uid) conninfo+=" user=$value" ;;
    password|pwd) conninfo+=" password='$value'" ;;
    sslmode) conninfo+=" sslmode=$(echo "$value" | tr '[:upper:]' '[:lower:]')" ;;
  esac
done

psql="/c/Program Files/PostgreSQL/15/bin/psql.exe"
[ -x "$psql" ] || psql="psql"
export PGOPTIONS="-c default_transaction_read_only=on -c statement_timeout=600000"
export PGCLIENTENCODING=UTF8
if [ "${1:-}" = "-f" ]; then
  "$psql" "$conninfo" -X -v ON_ERROR_STOP=1 -f "$2"
else
  "$psql" "$conninfo" -X -v ON_ERROR_STOP=1 -c "$1"
fi
