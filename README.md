# osu-server-replay-store

Handles all replay-related storage.

## Environment variables

For advanced testing purposes.

This project supports three environment setups.
The choice of the environment is steered by the `ASPNETCORE_ENVIRONMENT` environment variable.
Depending on environment, the configuration & config requirements change slightly.

- `ASPNETCORE_ENVIRONMENT=Development`:
  - Developer exception pages & API docs (`/api-docs`) are enabled.
  - Sentry & Datadog integrations are optional.
- `ASPNETCORE_ENVIRONMENT=Staging`:
   - Developer exception pages & API docs are disabled.
   - Sentry integration is mandatory.
   - Datadog integration is optional.
- `ASPNETCORE_ENVIRONMENT=Production`:
   - Developer exception pages & API docs are disabled.
   - Sentry & Datadog integrations are mandatory.

| Envvar name                   | Description                                                                                                                                                                                                                                         |              Mandatory?               | Default value |
|:------------------------------|:----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|:-------------------------------------:|:--------------|
| `DB_HOST`                     | Hostname under which the `osu-web` MySQL instance can be found.                                                                                                                                                                                     |                 ❌ No                  | `localhost`   |
| `DB_PORT`                     | Port under which the `osu-web` MySQL instance can be found.                                                                                                                                                                                         |                 ❌ No                  | `3306`        |
| `DB_USER`                     | Username to use when logging into the `osu-web` MySQL instance.                                                                                                                                                                                     |                 ❌ No                  | `root`        |
| `DB_PASS`                     | Password to use when logging into the `osu-web` MySQL instance.                                                                                                                                                                                     |                 ❌ No                  | `""`          |
| `DB_NAME`                     | Name of database to use on the indicated MySQL instance.                                                                                                                                                                                            |                 ❌ No                  | `osu`         |
| `REDIS_HOST`                     | Host name under which a Redis instance can be found.                                                                                                                                                                                           |                 ❌ No                  | `localhost`         |
| `REPLAY_STORAGE_TYPE`        | Which type of replay storage to use. Valid values are `local` and `s3`.                                                                                                                                                                            |                ✔️ Yes                 | None          |
| `REPLAY_CACHE_HOURS`                     | Cache duration for replays, in hours.                                                                                                                                                                                            |                 ❌ No                  | `24`         |
| `LOCAL_REPLAY_STORAGE_PATH`  | The path of a directory where solo replays should reside.                                                                                                                                                                                 |  ⚠️ If `REPLAY_STORAGE_TYPE=local`   | None          |
| `LOCAL_LEGACY_REPLAY_STORAGE_PATH`  | The path of a directory where legacy replays should reside.                                                                                                                                                                                 |  ⚠️ If `REPLAY_STORAGE_TYPE=local`   | None          |
| `S3_ACCESS_KEY`               | A valid Amazon S3 access key ID.                                                                                                                                                                                                                    |    ⚠ If `REPLAY_STORAGE_TYPE=s3`     | None          |
| `S3_SECRET_KEY`               | The secret key corresponding to the `S3_ACCESS_KEY`.                                                                                                                                                                                                |    ⚠ If `REPLAY_STORAGE_TYPE=s3`     | None          |
| `S3_REPLAYS_BUCKET_NAME`      | The name of the S3 bucket to use for solo replays.                                                                                                                                                         |    ⚠ If `REPLAY_STORAGE_TYPE=s3`     | None          |
| `S3_REPLAYS_BUCKET_REGION`    | The name of the region for the replay buckets.                                                                                                                                                                               |    ⚠ If `REPLAY_STORAGE_TYPE=s3`     | None          |
| `S3_LEGACY_REPLAYS_BUCKET_NAME`     | The name of the S3 bucket to use for legacy replays.                                                                                                                                                                             |    ⚠ If `REPLAY_STORAGE_TYPE=s3`     | None          |
| `SENTRY_DSN`                  | A valid Sentry DSN to use for logging application events.                                                                                                                                                                                           | ⚠ In staging & production environment | None          | 
| `DD_AGENT_HOST`               | A hostname pointing to a Datadog agent instance to which metrics should be reported.                                                                                                                                                                |      ⚠ In production environment      | None          |