[Back to README](../README.md)

# Development setup

Use [Setup and delivery](setup-and-delivery.md) for the reviewer checklist, test layers, CI jobs, security model, and known limitations. This document focuses on local execution.

## Prerequisites

Containerized execution requires Git and Docker Compose v2. Direct local development additionally requires:

- .NET SDK 8.0.416, pinned by global.json;
- Node.js >=22.22.3 <23;
- npm >=10 <11;
- the local EF Core tool restored from .config/dotnet-tools.json.

## Complete stack with Docker Compose

Copy the environment template and provide local-only values:

~~~powershell
Copy-Item .env.example .env
~~~

Required values:

~~~dotenv
POSTGRES_PASSWORD=<local-database-password>
DATABASE_CONNECTION_STRING=Host=database;Port=5432;Database=developer_evaluation;Username=developer;Password=<same-local-database-password>
JWT_SECRET_KEY=<random-value-containing-at-least-32-bytes>
~~~

To authenticate in the browser, enable the optional Development-only administrator:

~~~dotenv
DEVELOPMENT_ADMIN_ENABLED=true
DEVELOPMENT_ADMIN_EMAIL=admin@example.test
DEVELOPMENT_ADMIN_PASSWORD=<strong-local-password>
~~~

.env is ignored by Git. Start the stack:

~~~powershell
docker compose up --detach --build --wait --wait-timeout 240
docker compose ps
~~~

Compose starts PostgreSQL, MongoDB, Jaeger, the migration bundle, API, and Nginx-hosted Angular application. Stop it with docker compose down. Add --volumes only for an intentional data reset.

| Service | Host address |
|---|---|
| Frontend | http://localhost:4200 |
| API / Swagger | http://localhost:5119 / http://localhost:5119/swagger |
| PostgreSQL | localhost:5434 |
| MongoDB | localhost:27018 |
| Jaeger | http://localhost:16686 |

The ports can be overridden through the matching variables in .env.example.

## Rider or dotnet run

Start the persistence dependencies:

~~~powershell
docker compose up --detach database audit-database
dotnet tool restore
~~~

Configure secrets outside committed files:

~~~powershell
dotnet user-secrets set "Jwt:SecretKey" "<random-value-containing-at-least-32-bytes>" --project src/backend/Ambev.DeveloperEvaluation.WebApi
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5434;Database=developer_evaluation;Username=developer;Password=<local-database-password>" --project src/backend/Ambev.DeveloperEvaluation.WebApi
dotnet user-secrets set "MongoAudit:Enabled" "true" --project src/backend/Ambev.DeveloperEvaluation.WebApi
dotnet user-secrets set "MongoAudit:ConnectionString" "mongodb://localhost:27018" --project src/backend/Ambev.DeveloperEvaluation.WebApi
~~~

Configure DevelopmentAdmin values through User Secrets when a UI account is needed. The seed is disabled by default, idempotent by normalized email, and cannot run outside Development.

Apply migrations:

~~~powershell
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5434;Database=developer_evaluation;Username=developer;Password=<local-database-password>"
dotnet tool run dotnet-ef database update --project src/backend/Ambev.DeveloperEvaluation.ORM --startup-project src/backend/Ambev.DeveloperEvaluation.WebApi
Remove-Item Env:ConnectionStrings__DefaultConnection
~~~

Open Ambev.DeveloperEvaluation.sln in Rider and select the WebApi http profile, or run:

~~~powershell
dotnet run --project src/backend/Ambev.DeveloperEvaluation.WebApi
~~~

Both paths use http://localhost:5119 in Development.

## Angular frontend

Start the API first, then:

~~~powershell
Set-Location src/frontend
npm ci
npm start
~~~

Open http://localhost:4200. The development proxy forwards /api to http://localhost:5119.

The access token remains only in Angular memory. On application startup the frontend calls the refresh endpoint with credentials; a valid rotating HttpOnly refresh cookie restores the session after reload. Guards preserve a safe internal return URL for post-login navigation.

## Health checks

- /health/live checks the process without requiring PostgreSQL.
- /health/ready checks PostgreSQL and returns 503 while it is unavailable.
- /health includes diagnostic checks such as audit/outbox health.

MongoDB audit failure does not invalidate a committed sale; the PostgreSQL outbox retains pending work.

## Production configuration

Production must provide Jwt__SecretKey, Jwt__Issuer, Jwt__Audience, the PostgreSQL connection string, and explicit Cors__AllowedOrigins through its configuration or secret provider. HTTPS is required for the default __Host- refresh cookie.

Production refresh cookies are Secure, HttpOnly, SameSite=Strict, use Path=/, and carry the __Host- prefix. Startup validation rejects invalid JWT and cookie settings. Swagger and the development administrator remain Development-only.

On Windows, Docker can fail to resolve a checkout path containing decomposed Unicode characters. An ASCII-only checkout path avoids that engine limitation; the isolated E2E runner maps the current workspace automatically.
