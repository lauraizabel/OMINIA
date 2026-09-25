[Back to README](../README.md)

# Development setup

The API requires a JWT signing key with at least 32 bytes. Keep this key outside committed configuration.

## Rider or `dotnet run`

Configure the signing key in .NET User Secrets:

```powershell
dotnet user-secrets set "Jwt:SecretKey" "replace-with-a-random-secret-containing-at-least-32-characters" `
  --project src/backend/Ambev.DeveloperEvaluation.WebApi
dotnet user-secrets set "ConnectionStrings:DefaultConnection" `
  "Host=localhost;Port=5434;Database=developer_evaluation;Username=developer;Password=<local-database-password>" `
  --project src/backend/Ambev.DeveloperEvaluation.WebApi
```

Apply migrations and start the API:

```powershell
dotnet tool restore
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5434;Database=developer_evaluation;Username=developer;Password=<local-database-password>"
dotnet tool run dotnet-ef database update `
  --project src/backend/Ambev.DeveloperEvaluation.ORM `
  --startup-project src/backend/Ambev.DeveloperEvaluation.WebApi
Remove-Item Env:ConnectionStrings__DefaultConnection
dotnet run --project src/backend/Ambev.DeveloperEvaluation.WebApi
```

The explicit environment variable is used only by the EF design-time factory. Runtime configuration continues to come from User Secrets.

The Rider launch profile uses `http://localhost:5119`. PostgreSQL from Docker Compose is exposed on host port `5434`.

## Angular frontend

The frontend requires Node.js `22.22.3` or later in the Node 22 release line and npm 10. Start the API first, then run:

```powershell
cd src/frontend
npm ci
npm start
```

Open `http://localhost:4200`. The development server proxies `/api` to `http://localhost:5119`. Authentication tokens remain in memory and are never written to browser storage, so a page reload requires signing in again. The original internal deep link is restored after a successful login.

Run the frontend checks with:

```powershell
npm run test:ci
npm run build
```

Health probes are available without authentication:

- `/health/live` checks whether the API process can respond and does not depend on PostgreSQL.
- `/health/ready` checks PostgreSQL connectivity and returns `503 Service Unavailable` while the database is unavailable.
- `/health` reports both checks.

## Optional development administrator

The development-only seed is disabled by default and never runs in other environments. Enable it through User Secrets when a local administrator is needed:

```powershell
dotnet user-secrets set "DevelopmentAdmin:Enabled" "true" --project src/backend/Ambev.DeveloperEvaluation.WebApi
dotnet user-secrets set "DevelopmentAdmin:Email" "admin@example.com" --project src/backend/Ambev.DeveloperEvaluation.WebApi
dotnet user-secrets set "DevelopmentAdmin:Password" "choose-a-strong-local-password" --project src/backend/Ambev.DeveloperEvaluation.WebApi
```

The seed is idempotent by normalized email. It applies pending migrations and creates the administrator only when the configured email does not exist.

## Docker Compose

Copy `.env.example` to `.env` and provide local-only values. `POSTGRES_PASSWORD` and the password inside `DATABASE_CONNECTION_STRING` must match; `JWT_SECRET_KEY` must contain at least 32 bytes. Optionally enable the development administrator for UI authentication. The resulting file resembles:

```dotenv
POSTGRES_PASSWORD=<local-database-password>
DATABASE_CONNECTION_STRING=Host=database;Port=5432;Database=developer_evaluation;Username=developer;Password=<same-local-database-password>
JWT_SECRET_KEY=<random-key-with-at-least-32-bytes>
DEVELOPMENT_ADMIN_ENABLED=true
DEVELOPMENT_ADMIN_EMAIL=admin@example.test
DEVELOPMENT_ADMIN_PASSWORD=<strong-local-admin-password>
```

Start the complete stack:

```powershell
docker compose up --detach --build
```

The migration container must finish successfully before the API starts. Compose then waits for PostgreSQL, API, and frontend health checks. Open `http://localhost:4200`; API and Swagger remain available at `http://localhost:5119` and `http://localhost:5119/swagger`.

Stop the stack while preserving PostgreSQL data with `docker compose down`. Add `--volumes` when an explicit database reset is intended.

On Windows, Docker may fail to resolve a workspace whose path contains decomposed Unicode characters. Cloning into a regular ASCII path avoids the Docker limitation; the isolated E2E runner handles the current workspace automatically.

## Continuous integration

`.github/workflows/ci.yml` runs backend tests with PostgreSQL Testcontainers, frontend lint/typecheck/tests/build, the isolated Playwright suite, and a complete Compose smoke test. A dedicated job merges backend and frontend Cobertura data into a code-coverage summary displayed in the workflow run. Test results, combined coverage, and browser artifacts are retained for seven days. Pull-request runs are cancelled when a newer commit supersedes them.

Production must supply `Jwt__SecretKey`, `Jwt__Issuer`, and `Jwt__Audience` from its secret/configuration provider. The application fails during startup when JWT configuration is missing or invalid.
