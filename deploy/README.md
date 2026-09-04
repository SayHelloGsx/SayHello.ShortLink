# ShortLink VPS deployment

## Prerequisites

- A Linux VPS with Docker Engine and Docker Compose v2.
- A public DNS A/AAAA record for the short-link domain.
- Inbound TCP 80/443 and UDP 443 allowed.
- SMTP credentials for email confirmation.
- OpenSSL for the one-time OpenIddict certificate generation.

## First deployment

1. Copy `env.example` to `.env` and replace every placeholder with a unique secret.
2. Export the certificate password and generate the signing certificate:

   ```sh
   export OPENIDDICT_CERTIFICATE_PASSWORD='the-same-value-as-in-.env'
   ./scripts/generate-openiddict-certificate.sh
   ```

3. Build locally, or set `SHORTLINK_IMAGE` to an image published by CI.
4. Start PostgreSQL and Redis:

   ```sh
   docker compose up -d postgres redis
   ```

5. Run the one-shot database migrator:

   ```sh
   docker compose --profile migration run --rm dbmigrator
   ```

6. Start the web application and Caddy:

   ```sh
   docker compose up -d web caddy
   ```

7. Verify `https://YOUR_DOMAIN/health/ready`, register a test account, confirm its email,
   create a link, and open the resulting short URL.

## Updating

Pin `SHORTLINK_IMAGE` to a version or immutable commit SHA, then run:

```sh
./scripts/update.sh
```

Keep the previous image tag until the health check and smoke test pass. Roll back by restoring
the previous tag in `.env`, running the migrator only when the migration is backward compatible,
and recreating `web`.

## Backup and restore

Create a PostgreSQL custom-format backup:

```sh
./scripts/backup-postgres.sh
```

Test restores regularly on a separate environment:

```sh
./scripts/restore-postgres.sh /path/to/shortlink-TIMESTAMP.dump
```

Redis contains cache entries, rate-limit windows, and data-protection keys. Persist its volume,
but PostgreSQL remains the authoritative business-data store.
