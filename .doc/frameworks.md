[Back to README](../README.md)

# Frameworks and libraries

Only libraries used by the current implementation are listed.

## Backend

| Library | Role |
|---|---|
| ASP.NET Core | HTTP hosting, controllers, JWT authentication, authorization, rate limiting, health checks, and middleware. |
| MediatR | Dispatches commands and queries and hosts the request validation behavior. |
| FluentValidation | Validates application commands and selected HTTP request models. |
| Mapperly | Generates feature-local mappings at compile time. It replaced AutoMapper; no runtime mapper registration is required. |
| Entity Framework Core / Npgsql | PostgreSQL mappings, migrations, transactions, concurrency, projections, and repositories. |
| MongoDB.Driver | Writes and queries the idempotent sale-event audit projection. |
| BCrypt.Net-Next | Hashes user passwords. |
| Serilog | Structured logging and exception enrichment. |
| OpenTelemetry | Traces ASP.NET Core, HTTP, PostgreSQL, frontend requests, and outbox delivery. |
| Swashbuckle | Generates Swagger/OpenAPI discovery in Development. |

No service-bus framework is installed. Event delivery uses the PostgreSQL outbox worker; Rebus is not part of the implementation.

## Testing

| Library | Role |
|---|---|
| xUnit | Backend unit, integration, and functional test runner. |
| NSubstitute | Test doubles for isolated backend tests. |
| FluentAssertions | Readable assertions in unit tests. |
| Bogus | Test-data generation where varied data is useful. |
| Testcontainers | Disposable PostgreSQL and MongoDB integration dependencies. |
| Vitest and Angular TestBed | Frontend unit and component tests. |
| Playwright | Chromium browser E2E, accessibility, concurrency, and resilience scenarios. |
