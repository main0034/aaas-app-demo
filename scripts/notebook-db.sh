#!/usr/bin/env bash
# Local development only. Starts Postgres and applies the migrations, then
# exits - the app itself runs on the host (Rider or `dotnet run`), because the
# `claude` CLI and its login live there. Rider runs this before "Notebook"
# (.run/), and it works the same from a terminal:
#
#   ./scripts/notebook-db.sh
#
# Idempotent: re-running it rebuilds the migration image from the checked-out
# code and applies whatever migrations the branch has added.
set -euo pipefail
cd "$(dirname "$0")/.."

# An IDE started from the Dock may not have Docker's CLI on PATH.
export PATH="$PATH:/usr/local/bin:/opt/homebrew/bin:/Applications/Docker.app/Contents/Resources/bin"

if ! docker info >/dev/null 2>&1; then
  echo "Docker is not running. Start Docker Desktop and run this again." >&2
  exit 1
fi

docker compose build migrate
docker compose up -d db
# `run` starts db's dependencies first and waits for its healthcheck, then runs
# the one-shot migration and returns its exit code - which is what lets Rider
# stop before starting the app if a migration fails.
docker compose run --rm -T migrate
echo "Postgres is up on localhost:5432 and migrations are applied."
