#!/usr/bin/env sh
set -eu

if [ -z "${OPENIDDICT_CERTIFICATE_PASSWORD:-}" ]; then
    echo "Set OPENIDDICT_CERTIFICATE_PASSWORD before running this script." >&2
    exit 1
fi

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
secrets_dir="$script_dir/../secrets"
mkdir -p "$secrets_dir"

key_file=$(mktemp)
certificate_file=$(mktemp)
trap 'rm -f "$key_file" "$certificate_file"' EXIT

openssl req \
    -x509 \
    -newkey rsa:4096 \
    -sha256 \
    -days 825 \
    -nodes \
    -subj "/CN=ShortLink OpenIddict" \
    -keyout "$key_file" \
    -out "$certificate_file"

openssl pkcs12 \
    -export \
    -out "$secrets_dir/openiddict.pfx" \
    -inkey "$key_file" \
    -in "$certificate_file" \
    -passout "pass:$OPENIDDICT_CERTIFICATE_PASSWORD"

chmod 600 "$secrets_dir/openiddict.pfx"
echo "Created $secrets_dir/openiddict.pfx"
