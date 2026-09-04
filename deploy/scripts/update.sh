#!/usr/bin/env sh
set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
deploy_dir="$script_dir/.."
cd "$deploy_dir"

docker compose pull web dbmigrator
docker compose --profile migration run --rm dbmigrator
docker compose up -d --no-deps web
docker compose up -d caddy
docker compose ps
