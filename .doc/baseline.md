[Back to README](../README.md)

## Development baseline

Baseline captured on September 23, 2026 after consolidating the template under `src/backend` and `tests/backend`.

### Toolchain

| Tool | Version |
|---|---:|
| .NET SDK | 8.0.416 (pinned by `global.json`) |
| Docker Engine | 29.1.3 |
| Node.js | 22.23.1 |
| npm | 10.9.8 |
| PostgreSQL image | 16.15-alpine |
| Entity Framework CLI | 8.0.10 (local tool manifest) |

### Verified commands

```powershell
dotnet tool restore
dotnet restore Ambev.DeveloperEvaluation.sln
dotnet build Ambev.DeveloperEvaluation.sln --configuration Release --no-restore
dotnet test Ambev.DeveloperEvaluation.sln --configuration Release --no-build --no-restore
dotnet tool run dotnet-ef database update `
  --project src/backend/Ambev.DeveloperEvaluation.ORM/Ambev.DeveloperEvaluation.ORM.csproj `
  --startup-project src/backend/Ambev.DeveloperEvaluation.WebApi/Ambev.DeveloperEvaluation.WebApi.csproj `
  --configuration Release --no-build
docker compose up --detach --build
```

Observed results:

- Restore completed from NuGet.
- Release build completed with zero errors and one known dependency warning.
- 49 inherited unit tests passed with no skipped or failed tests.
- The Integration and Functional projects compile but do not contain tests yet.
- PostgreSQL became healthy. Migrations `20241014011203_InitialMigrations` and `20260924001411_AddUserTimestamps` created the `Users` schema and `__EFMigrationsHistory`.
- The container image built successfully, the API and database started through Compose, and `/swagger/index.html` returned HTTP 200 on port 8080.
- A database-backed smoke flow successfully created, read, and deleted a temporary user.

PostgreSQL is published on host port 5434 to avoid common conflicts with an existing local PostgreSQL on 5432. Containers use port 5432 inside the Compose network.

### Known baseline risks

- AutoMapper 13.0.1 is affected by [GHSA-rvv3-g6hj-g44x](https://github.com/advisories/GHSA-rvv3-g6hj-g44x). Patched releases start at 15.1.1, but AutoMapper 15 also introduces a license requirement. The project will replace AutoMapper with explicit mappings instead of accepting a new commercial dependency during baseline setup.
- Docker Desktop on Windows may fail to resolve a checkout path that contains decomposed Unicode characters. The current workspace was validated through a temporary ASCII junction. A normal checkout in an ASCII-only path, such as `D:\work\ambev-evaluation`, does not require this workaround.
- The inherited user API still returns HTTP 500 instead of HTTP 404 when a deleted or unknown user is queried. Error normalization belongs to the API protection work and must be fixed before delivery.
- The development JWT secret and database password are template-only local values. Production configuration must provide secrets externally.
