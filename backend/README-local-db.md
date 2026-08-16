# Local database

The API talks to a real PostgreSQL everywhere — locally, in tests, and on Render. There is no
in-memory provider path and there should not be one: an in-memory provider does not enforce
`numeric(18,2)`, does not have `timestamptz`, and does not run the migration, so it would pass
tests for schema decisions it cannot represent. The Docker path is the deployed path.

## Start it

```bash
docker run -d --name aniko-pg \
  -e POSTGRES_USER=aniko -e POSTGRES_PASSWORD=aniko_dev -e POSTGRES_DB=aniko \
  -p 55432:5432 postgres:17-alpine
```

Port **55432**, not 5432, so it cannot collide with a Postgres already installed on the host.

Major version **17**, matching `postgresMajorVersion: "17"` in `render.yaml`. These are pinned
together on purpose — a migration developed against one major version and deployed onto another
is untested, and Render's version cannot be changed after the database is created.

## Apply and revert the migration

```bash
cd backend
dotnet ef database update --project AniKo_API
dotnet ef database update 0 --project AniKo_API   # revert to empty
```

Both directions get run. A migration that has never been reverted is an untested migration —
the `Down` path is generated code that nobody reads and everybody assumes works.

## Credentials

The password here guards nothing. The container is local, disposable, and holds only seeded
demo data. It is committed in `appsettings.Development.json` so a fresh clone runs without a
setup step. Production never reads that key: Render injects `DATABASE_URL`, which
`ConnectionStringResolver` prefers over it.

## Throw it away

```bash
docker rm -f aniko-pg
```

Nothing in the container is worth keeping — the schema comes from migrations and the data from
the seeder, both of which are in source control.
