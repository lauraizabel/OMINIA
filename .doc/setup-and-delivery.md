[Back to README](../README.md)

# Setup and delivery

This is the reviewer-oriented guide for the implemented sales solution. Use [development-setup.md](development-setup.md) for detailed local configuration and [sales-api.md](sales-api.md) for the domain and HTTP contract.

## Delivered scope

- .NET 8 API organized into Domain, Application, ORM, IoC, Common, and WebApi projects.
- PostgreSQL persistence with EF Core migrations and optimistic concurrency.
- Angular authentication and complete sales list/detail/create/edit/cancel/delete workflow.
- Transactional event outbox with idempotent MongoDB audit projection.
- Rotating refresh sessions, role authorization, rate limiting, origin checks, and structured errors.
- Docker Compose stack, OpenAPI, Postman, health checks, and OpenTelemetry/Jaeger.
- Automated unit, integration, functional/API, frontend unit, and Playwright E2E tests.

Customer, branch, and product catalogs are outside this challenge. Sales store external ID/name snapshots so historical descriptions do not depend on those systems.

## Reviewer quick start

Windows PowerShell:

~~~powershell
Copy-Item .env.example .env
~~~

Linux or macOS (bash/zsh):

~~~bash
cp .env.example .env
~~~

Fill POSTGRES_PASSWORD, DATABASE_CONNECTION_STRING, and JWT_SECRET_KEY. To use the UI, set DEVELOPMENT_ADMIN_ENABLED=true plus a local email and password. Then run:

The following command is the same in Windows PowerShell, Linux, and macOS:

~~~shell
docker compose up --detach --build --wait --wait-timeout 240
~~~

| Resource | URL |
|---|---|
| Frontend | http://localhost:4200 |
| API | http://localhost:5119 |
| Swagger | http://localhost:5119/swagger |
| Liveness / readiness | http://localhost:5119/health/live / http://localhost:5119/health/ready |
| Jaeger | http://localhost:16686 |

A useful review path is: authenticate, create a four-unit line and confirm 10%, change it to ten units and confirm 20%, exercise list filters, open the same sale in two tabs to observe stale ETag handling, cancel an item, and soft-delete a disposable sale.

## Architecture

The HTTP and project dependency diagrams are in [project-structure.md](project-structure.md). PostgreSQL is the authoritative store. Sale changes and outbox messages commit atomically. The background processor leases pending messages, preserves per-aggregate order, retries with bounded backoff, writes MongoDB documents keyed by event ID, and then acknowledges delivery.

Administrator-only operations expose outbox backlog/dead-letter metadata and permit explicit replay. Event payloads are not returned by the operational endpoint.

## Authentication and security

- Authentication issues a 15-minute JWT and an opaque refresh token.
- The frontend holds the JWT only in memory.
- The refresh token is sent through an HttpOnly, SameSite=Strict cookie. Production enables Secure and requires the __Host- cookie name.
- PostgreSQL stores the SHA-256 refresh-token hash, not the raw token.
- Refresh rotates the session. Reuse outside the configured grace period revokes the token family.
- Logout revokes the family and clears the cookie.
- Login and session endpoints are rate-limited by remote address.
- Cookie-backed auth endpoints validate a supplied Origin header against the request origin or configured CORS origins.
- Sales require Manager or Admin; user administration and outbox operations require Admin.
- Unknown JSON members and request bodies above 256 KiB are rejected; unexpected failures do not return stack traces.

These controls reduce token exposure and detect replay. Production still requires HTTPS, explicit trusted origins, externally managed secrets, and an appropriate operational security boundary.

## Testing

### Backend

These commands are the same in Windows PowerShell, Linux, and macOS:

~~~shell
dotnet build Ambev.DeveloperEvaluation.sln -c Release
dotnet test Ambev.DeveloperEvaluation.sln -c Release --no-build
~~~

- Unit tests isolate domain rules, handlers, mappings, and supporting services.
- Integration tests use Testcontainers for PostgreSQL and MongoDB persistence, concurrency, outbox, and audit behavior.
- Functional tests exercise the ASP.NET Core pipeline, validation, HTTP errors, auth, authorization, health, and controller contracts.

### Frontend

~~~shell
cd src/frontend
npm ci
npm run lint
npx tsc -p tsconfig.app.json --noEmit
npm run test:ci -- --coverage
npm run build
~~~

Vitest and Angular TestBed cover services, guards, interceptors, forms, components, and templates.

### Playwright E2E

~~~shell
npm run test:e2e:install
npm run test:e2e
~~~

The isolated runner generates credentials in memory, starts disposable PostgreSQL and API containers, launches Angular, runs the Chromium scenarios, and removes containers and volumes. The suite covers authentication/session restoration, lifecycle rules, concurrency, interrupted responses, accessibility, and telemetry. See [the E2E matrix](../src/frontend/e2e/README.md).

## CI pipeline

.github/workflows/ci.yml runs on pull requests and pushes to main:

| Job | Checks |
|---|---|
| Backend tests | Restore, vulnerable NuGet package audit, Release build, all .NET tests, Cobertura collection, TRX artifacts. |
| Frontend checks | npm ci, ESLint, TypeScript typecheck, Vitest coverage/JUnit, production build. |
| Code coverage | Independent ReportGenerator summaries and 90% line and branch gates for backend and frontend. |
| Isolated browser suite | Chromium installation, Playwright E2E, JUnit and failure artifacts. |
| Container stack smoke | Ephemeral credentials, Compose build/start, frontend, readiness, authentication, OTLP proxy, and queryable Jaeger trace. |

Artifacts are retained for seven days. New runs cancel older runs for the same branch. This is a CI pipeline; the repository does not deploy an environment.

## API review resources

- [Sales contract](sales-api.md)
- [OpenAPI document](openapi/sales.yaml)
- [Postman collection](postman/DeveloperStore-Sales.postman_collection.json) and [workflow](postman/README.md)
- [Runnable HTTP requests](../src/backend/Ambev.DeveloperEvaluation.WebApi/Ambev.DeveloperEvaluation.WebApi.http)
- Swagger UI in Development

The Postman scripts authenticate and capture the JWT, generated sale IDs, item IDs, and ETags used by later requests.

## Relevant decisions

- Sale owns its lines and all rules affecting totals or state.
- Discounts and monetary totals are calculated server-side with decimal arithmetic and two-digit rounding.
- Strong ETags and If-Match prevent silent last-write-wins updates.
- Cancellation is an irreversible business transition; deletion is a separate tombstone operation.
- List queries project only summary fields, use allowlisted ordering, and do not load item collections.
- Mapperly generates compile-time mappings. AutoMapper was removed after a security advisory.
- PostgreSQL outbox plus MongoDB audit avoids coupling a successful sale write to audit availability.
- A broker and Redis are unnecessary for this single-service delivery; the outbox provides durable work and replay.
- OpenTelemetry propagates W3C trace context through browser, API, PostgreSQL, outbound HTTP, and later outbox processing.

## Known limitations

- Audit delivery is eventually consistent and at least once. Exhausted retries require an administrator replay after MongoDB is repaired.
- Create operations do not support persistent idempotency keys; interrupted responses can leave the client uncertain whether the write committed.
- External customer, branch, and product catalog services and lookup UIs are outside the challenge.
- No cloud infrastructure, deployment automation, managed secret store, or external message broker is included.
- Local Jaeger is reproducible review infrastructure, not a production observability topology.
- Swagger is enabled only in Development.

## Review checklist

1. Start the stack from a clean checkout with local values in .env.
2. Exercise the discount, concurrency, cancellation, and soft-delete flows.
3. Run backend, frontend, and isolated E2E commands.
4. Confirm Backend tests, Frontend checks, Code coverage, Isolated browser suite, Container stack smoke, and secret scanning are green.
5. Confirm no real credentials were committed.
6. Remove local state with docker compose down --volumes --remove-orphans when review is complete.
