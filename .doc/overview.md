[Back to README](../README.md)

# Overview

This repository delivers the DeveloperStore sales challenge as a complete backend and browser workflow. The code is organized around a Sale aggregate that owns discounts, totals, cancellation, deletion state, optimistic versioning, and domain events.

## Delivered scope

- **Layered architecture:** HTTP concerns, use-case orchestration, domain rules, and persistence are kept in separate projects with explicit references.
- **Domain modeling:** the Sale aggregate owns discounts, totals, snapshots, cancellation, soft deletion, versions, and domain events.
- **Validation:** FluentValidation runs application validators through the MediatR pipeline; the domain independently protects its invariants.
- **Concurrency:** strong ETags and `If-Match` reject stale sale mutations instead of silently overwriting them.
- **Persistence and events:** EF Core persists authoritative state and the transactional outbox in PostgreSQL; a background processor writes an idempotent MongoDB audit projection.
- **Authentication and security:** short-lived JWTs, rotating `HttpOnly` refresh sessions, role policies, origin checks, rate limits, and bounded request bodies protect the HTTP surface.
- **Testing:** unit, PostgreSQL/MongoDB integration, functional/API, Angular, and Playwright suites cover the main boundaries.
- **Observability:** OpenTelemetry correlates browser, API, PostgreSQL, HTTP, and outbox activity, with Jaeger included for local trace review.

External customer, branch, and product systems are represented by ID/name snapshots. PostgreSQL owns sale state and totals; MongoDB stores audit history produced asynchronously from domain events.

## Suggested review path

1. Read the [root README](../README.md) for requirements and quick start.
2. Review [project structure](project-structure.md) for dependencies and event flow.
3. Use [sales-api.md](sales-api.md) for the business and HTTP contract.
4. Follow [setup-and-delivery.md](setup-and-delivery.md) to run and assess the solution.
5. Inspect [dependency-inventory.md](dependency-inventory.md) for the Mapperly migration and dependency audit.

The implementation and automated tests are the source of truth when a document and code disagree.
