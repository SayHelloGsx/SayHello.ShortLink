#!/usr/bin/env sh
set -eu

if [ "$#" -ne 1 ] || [ ! -f "$1" ]; then
    echo "Usage: $0 /path/to/backup.dump" >&2
    exit 1
fi

backup_file=$(CDPATH= cd -- "$(dirname -- "$1")" && pwd)/$(basename -- "$1")
script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
deploy_dir="$script_dir/.."
cd "$deploy_dir"

set -a
. ./.env
set +a

cat "$backup_file" | docker compose exec -T postgres \
    pg_restore \
    --username "$POSTGRES_USER" \
    --dbname "$POSTGRES_DB" \
    --clean \
    --if-exists \
    --no-owner

echo "Restore completed from $backup_file"
