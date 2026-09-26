[Back to README](../README.md)

# Technology stack

| Area | Technology | Current use |
|---|---|---|
| Backend runtime | .NET 8 / C# | ASP.NET Core API and background services; SDK 8.0.416 is pinned in global.json. |
| Frontend runtime | Angular 22 / TypeScript 6 / RxJS 7 | Sales portal, in-memory session state, forms, routing, and HTTP integration. |
| Node toolchain | Node.js >=22.22.3 <23, npm 10 | Frontend build, lint, unit tests, and Playwright. |
| Primary database | PostgreSQL 16 | Users, sales, refresh-session hashes, EF migrations, and transactional outbox. |
| Audit database | MongoDB 8 | Eventually consistent, idempotent sale-event audit projection. |
| Local telemetry | OpenTelemetry and Jaeger 2 | Correlated browser, API, database, HTTP, and outbox traces. |
| Containers | Docker Compose v2 | PostgreSQL, MongoDB, Jaeger, migrations, API, and frontend stack. |
| CI | GitHub Actions | Build, dependency audit, lint, typecheck, tests, coverage gates, Playwright, and Compose smoke. |

Package responsibilities are documented in [frameworks.md](frameworks.md). Exact package versions are defined by the project files and package-lock.json; container versions are defined in docker-compose.yml.
