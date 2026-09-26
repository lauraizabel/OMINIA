[Back to README](../README.md)

# Frameworks and libraries

Only libraries used by the current implementation are listed.

## Backend

| Library | Role |
|---|---|
| ASP.NET Core | HTTP hosting, controllers, JWT authentication, authorization, rate limiting, health checks, and middleware. |
| MediatR | Dispatches commands and queries and hosts the request validation behavior. |
| FluentValidation | Validates application commands and selected HTTP request models. |
| Mapperly | Generates feature-local mappings at compile time without a runtime mapper service. |
| BCrypt.Net-Next | Hashes user passwords. |
| Serilog | Structured logging and exception enrichment. |
| OpenTelemetry | Traces ASP.NET Core, HTTP, PostgreSQL, frontend requests, and outbox delivery. |
| Swashbuckle | Generates Swagger/OpenAPI discovery in Development. |

## Persistence

| Library | Role |
|---|---|
| Entity Framework Core | Maps aggregates, runs migrations, controls transactions and optimistic concurrency, and projects read queries. |
| Npgsql | Connects EF Core to PostgreSQL, the authoritative store for users, sales, refresh sessions, and the outbox. |
| MongoDB.Driver | Writes and queries the idempotent sale-event audit projection. |

## Frontend

| Library | Role |
|---|---|
| Angular | Implements routing, reactive forms, guards, interceptors, and the sales user interface. |
| RxJS | Coordinates HTTP requests, shared refresh-session recovery, loading states, and component lifecycles. |
| Vitest and Angular TestBed | Run frontend unit, component, template, guard, service, and interceptor tests. |
| Playwright | Exercises the authenticated browser workflow, concurrency, resilience, accessibility, and telemetry. |

## Testing

| Library | Role |
|---|---|
| xUnit | Backend unit, integration, and functional test runner. |
| NSubstitute | Test doubles for isolated backend tests. |
| FluentAssertions | Readable assertions in unit tests. |
| Bogus | Test-data generation where varied data is useful. |
| Testcontainers | Disposable PostgreSQL and MongoDB integration dependencies. |
