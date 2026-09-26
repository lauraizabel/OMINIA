[Back to README](../README.md)

# Overview

This repository delivers the DeveloperStore sales challenge as a complete backend and browser workflow. The code is organized around a Sale aggregate that owns discounts, totals, cancellation, deletion state, optimistic versioning, and domain events.

## Delivered scope

- ASP.NET Core API for sales creation, detail, listing, update, cancellation, item cancellation, and soft deletion.
- Angular application for authentication and the complete sales workflow.
- PostgreSQL persistence through EF Core migrations and repositories.
- Transactional outbox with an idempotent MongoDB audit projection.
- JWT authorization for Manager and Admin, plus rotating refresh sessions in secure cookies.
- OpenAPI, Postman, health checks, structured errors, OpenTelemetry traces, and Docker Compose.
- Unit, integration, functional/API, frontend unit, and Playwright E2E tests.

External customer, branch, and product systems are represented by ID/name snapshots. PostgreSQL owns sale state and totals; MongoDB stores audit history produced asynchronously from domain events.

## Suggested review path

1. Read the [root README](../README.md) for requirements and quick start.
2. Review [project structure](project-structure.md) for dependencies and event flow.
3. Use [sales-api.md](sales-api.md) for the business and HTTP contract.
4. Follow [setup-and-delivery.md](setup-and-delivery.md) to run and assess the solution.
5. Inspect [dependency-inventory.md](dependency-inventory.md) for the Mapperly migration and dependency audit.

The implementation and automated tests are the source of truth when a document and code disagree.
