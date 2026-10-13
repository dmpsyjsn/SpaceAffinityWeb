# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with the API in this folder. Repo-wide context (routing, images, smoke test, deployment) is in [../CLAUDE.md](../CLAUDE.md).

## Commands

Run from this folder.

```bash
# Postgres for local dev (credentials match appsettings.Development.json)
docker run -d --name spaceaffinity-pg -p 5432:5432 \
  -e POSTGRES_USER=app -e POSTGRES_PASSWORD=devpassword -e POSTGRES_DB=appdb postgres:17

dotnet run --launch-profile http   # http://localhost:5247
dotnet build
```

The dev connection string uses `Host=host.docker.internal` (Docker Desktop). Elsewhere, change it to `localhost`.

There is no unit test project. The only automated test is the image smoke test described in the root file.

## Architecture

Everything is wired in [Program.cs](Program.cs): DI, auth, startup schema creation and all endpoints. There are no controllers.

### Native AOT shapes most decisions

`PublishAot` is on, so reflection-dependent code either fails to compile cleanly or, worse, fails at runtime on the first call to that code path. A local `dotnet run` uses the JIT and will not reveal these problems. Only the published image does, which is why CI smoke tests real endpoints.

- **Every type that crosses the HTTP boundary must be registered** with a `[JsonSerializable]` attribute on [AppJsonSerializerContext](Context/AppJsonSerializerContext.cs). A missing entry works under `dotnet run` and throws in the container.
- The app uses `WebApplication.CreateSlimBuilder` and `NpgsqlSlimDataSourceBuilder`. Both omit features by default (the Npgsql one drops optional type plugins), so new capabilities may need to be opted into explicitly.
- Treat any new IL/trim/AOT analyzer warning as a bug.

### Data access

There is no ORM. The pattern is in [Context/](Context/):

- `SqlEntityMap<T>` describes a table without reflection: table and key names, the non-key `Columns`, idempotent `CreateTableSql`, and explicit `Read` / `Bind` / `HasKey` delegates.
- `PostgresPlainSqlContext<T>` builds its SELECT / INSERT / UPDATE strings from that map and implements `IPlainSqlContext<T>`.
- [SpaceNoteMap](Context/SpaceNoteMap.cs) is the one concrete map.

The map's pieces are coupled by position: `Read` gets the key at ordinal 0 followed by `Columns` in order, and `Bind` must add parameters named `p0..pN` in that same order, plus `p_key` when the item has a key. Changing a column means updating `Columns`, `Read`, `Bind` and `CreateTableSql` together.

To add an entity: write a map, register it as a singleton, register `IPlainSqlContext<T>`, and call `EnsureTableExists()` for it at startup.

Schema is created at startup with `CREATE TABLE IF NOT EXISTS` inside a transaction holding a Postgres advisory lock, so replicas starting together don't race. There is no migration tooling, and an existing table is never altered, so a schema change needs its own idempotent DDL.

`UpsertItem` inserts when the key is 0 and updates otherwise, returning `-1` when an update matched no row. It never inserts a caller-supplied key, because that would desync the identity sequence. `POST /api/spacenotes` relies on this: one endpoint handles both add (201) and edit (200, or 404 from the `-1`).

### Auth

`POST /api/login` checks a single username/password pair from configuration and returns a one-hour HS256 JWT. Endpoints opt in with `[Authorize]`; reads are public. All five `Authentication:*` keys (`JwtKey`, `JwtIssuer`, `JwtAudience`, `Username`, `Password`) are read with `!` at startup and have no defaults.

### Configuration

Values come from different places per environment, and nothing validates that they are present:

| Environment | Source |
|---|---|
| Local dev | [appsettings.Development.json](appsettings.Development.json), plus .NET user secrets for `Authentication:JwtKey`, `Authentication:Username` and `Authentication:Password` |
| Smoke test | `-e` flags in [smoke-test.sh](../.github/scripts/smoke-test.sh) |
| Cluster | `env:` in [k8s/api.yaml](../k8s/api.yaml), with secrets via `secretKeyRef` |

User secrets live outside the repo and only load in the Development environment, so a fresh clone has to set them once (the key needs at least 32 bytes for HS256):

```bash
dotnet user-secrets set "Authentication:JwtKey" "<at least 32 characters>"
dotnet user-secrets set "Authentication:Username" "<login username>"
dotnet user-secrets set "Authentication:Password" "<login password>"
```

Adding or renaming a required setting means updating all three (nested keys become `Section__Key` as env vars). Likewise, changing an endpoint's contract or auth requirement means updating the smoke test in the same change.
