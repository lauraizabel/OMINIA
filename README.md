# DeveloperStore Sales Evaluation

## Challenge / Requirements

This repository implements the Ambev Developer Evaluation sales challenge. The original delivery requirements are:

- deliver the solution within seven calendar days;
- publish the source in a public GitHub repository;
- provide instructions to configure, run, and test the project;
- implement a complete sales CRUD API using DDD-inspired boundaries and external identity snapshots.

A sale must expose its number, date, customer, branch, products, quantities, unit prices, discounts, line totals, sale total, and cancellation state. Publishing SaleCreated, SaleModified, SaleCancelled, and ItemCancelled events is an optional differential; no external message broker is required.

### Business rules

- 1-3 identical items receive no discount.
- 4-9 identical items receive a 10% discount.
- 10-20 identical items receive a 20% discount.
- More than 20 identical items is rejected.

The challenge text says both "above 4" and "4+". This implementation follows the explicit tier table: exactly four units receive 10%. The full contract is in [Sales API contract](.doc/sales-api.md).

## Engineering Highlights

- Layered, DDD-inspired backend with business invariants enforced by the Sale aggregate.
- MediatR commands and queries with FluentValidation pipeline behavior.
- PostgreSQL and EF Core persistence with stable projected list queries.
- Strong ETags and If-Match optimistic concurrency for sale mutations.
- Soft deletion and explicit sale/item cancellation semantics.
- Transactional PostgreSQL outbox and idempotent MongoDB audit projection.
- Mapperly compile-time mappings; no runtime reflection-based mapper.
- Short-lived JWTs and rotating refresh sessions in HttpOnly cookies.
- Angular workflow with in-memory access tokens and session restoration.
- Unit, integration, functional/API, Angular, and Playwright E2E tests.
- Docker Compose, OpenAPI, Postman, OpenTelemetry/Jaeger, and GitHub Actions CI.
- Independent backend and frontend line/branch coverage gates of 90%.

## Architecture

The main request flow is WebApi to Application to Domain, with the ORM implementing persistence and application ports. PostgreSQL is the source of truth. Sale events are committed to the outbox with aggregate changes and later projected into MongoDB by a background worker.

See [Project structure](.doc/project-structure.md) for dependency boundaries and diagrams, and [Setup and delivery](.doc/setup-and-delivery.md) for the reviewer workflow.

## Quick Start

Requirements: Docker Engine or Docker Desktop with Docker Compose v2.

~~~powershell
Copy-Item .env.example .env
~~~

Set POSTGRES_PASSWORD, DATABASE_CONNECTION_STRING, and JWT_SECRET_KEY in .env. To sign in through the UI, also enable and configure the development administrator. The database password in both PostgreSQL settings must match, and the JWT key must contain at least 32 bytes.

~~~powershell
docker compose up --detach --build --wait --wait-timeout 240
~~~

Use docker compose down to stop the stack, or add --volumes only when a full local reset is intended. Rider, migration, User Secrets, and troubleshooting instructions are in [Development setup](.doc/development-setup.md).

## Main URLs

| Resource | URL |
|---|---|
| Angular application | http://localhost:4200 |
| API | http://localhost:5119 |
| Swagger UI | http://localhost:5119/swagger |
| Liveness | http://localhost:5119/health/live |
| Readiness | http://localhost:5119/health/ready |
| Jaeger | http://localhost:16686 |

## Testing

Backend build and all .NET test projects:

~~~powershell
dotnet build Ambev.DeveloperEvaluation.sln -c Release
dotnet test Ambev.DeveloperEvaluation.sln -c Release --no-build
~~~

Frontend lint, typecheck, unit tests with coverage, and production build:

~~~powershell
Set-Location src/frontend
npm ci
npm run lint
npx tsc -p tsconfig.app.json --noEmit
npm run test:ci -- --coverage
npm run build
~~~

Playwright starts disposable PostgreSQL and API containers and removes them afterward. Docker must be available:

~~~powershell
npm run test:e2e:install
npm run test:e2e
~~~

Unit tests verify isolated rules and handlers; integration tests exercise PostgreSQL, MongoDB, and infrastructure behavior; functional tests validate HTTP/security behavior; Playwright verifies the browser-to-API workflow. See [Setup and delivery](.doc/setup-and-delivery.md#testing) and the [E2E guide](src/frontend/e2e/README.md).

## Documentation

| Document | Purpose |
|---|---|
| [Overview](.doc/overview.md) | Delivered scope and review path |
| [Project structure](.doc/project-structure.md) | Layer responsibilities and dependency flow |
| [Technology stack](.doc/tech-stack.md) | Runtime and platform inventory |
| [Frameworks and libraries](.doc/frameworks.md) | Libraries grouped by responsibility |
| [Development setup](.doc/development-setup.md) | Local, Rider, Docker, migrations, and configuration |
| [Setup and delivery](.doc/setup-and-delivery.md) | Reviewer workflow, testing, CI, security, and limitations |
| [Sales API contract](.doc/sales-api.md) | Business rules, HTTP contract, concurrency, and errors |
| [OpenAPI](.doc/openapi/sales.yaml) | Machine-readable sales contract |
| [Postman workflow](.doc/postman/README.md) | Ordered authenticated API walkthrough |
| [Observability](.doc/observability.md) | OpenTelemetry and Jaeger flow |
| [Frontend](src/frontend/README.md) | Angular development and authentication behavior |

## Relevant Technical Decisions

- PostgreSQL remains the source of truth; MongoDB stores the eventually consistent audit projection.
- The outbox is transactional and delivered at least once. MongoDB uses the event ID for idempotent writes.
- Access tokens are kept only in frontend memory. The opaque refresh token is stored in an HttpOnly, SameSite=Strict cookie and its hash is persisted.
- Refresh rotation detects replay outside a short concurrency grace period and revokes the token family.
- Sale updates use strong ETags to prevent silent last-write-wins behavior.
- Customer, branch, and product data are external identity snapshots; catalog services are outside the challenge scope.
- AutoMapper was replaced by Mapperly after a security advisory; the rationale is in [Dependency inventory](.doc/dependency-inventory.md).
- This repository provides a CI pipeline and reproducible local stack. It does not contain deployment automation or cloud infrastructure.

## Postman

Import the [collection](.doc/postman/DeveloperStore-Sales.postman_collection.json) and [local environment](.doc/postman/DeveloperStore-Local.postman_environment.json), select DeveloperStore - Local, and set the secret adminPassword value. The collection captures the JWT, sale IDs, item IDs, and ETags automatically.
