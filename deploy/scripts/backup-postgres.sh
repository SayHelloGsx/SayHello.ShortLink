#!/usr/bin/env sh
set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
deploy_dir="$script_dir/.."
cd "$deploy_dir"

if [ ! -f .env ]; then
    echo "Missing deploy/.env" >&2
    exit 1
fi

set -a
. ./.env
set +a

mkdir -p backups
timestamp=$(date -u +%Y%m%dT%H%M%SZ)
output="backups/shortlink-$timestamp.dump"

docker compose exec -T postgres \
    pg_dump \
    --username "$POSTGRES_USER" \
    --dbname "$POSTGRES_DB" \
    --format custom \
    --no-owner > "$output"

echo "Backup written to $deploy_dir/$output"
