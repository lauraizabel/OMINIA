[Back to README](../README.md)

# Project structure

## Backend

| Project | Responsibility and boundary |
|---|---|
| Ambev.DeveloperEvaluation.Domain | Owns aggregates, value objects, domain services, invariants, events, repository contracts, and domain exceptions. It may use Common, but it does not know HTTP, EF Core, MongoDB, or UI concerns. |
| Ambev.DeveloperEvaluation.Application | Defines MediatR commands/queries, validators, handlers, results, and application ports. It coordinates domain abstractions and does not depend on WebApi or concrete persistence. |
| Ambev.DeveloperEvaluation.ORM | Implements EF Core repositories, mappings, migrations, unit of work, refresh-session persistence, the transactional outbox, and MongoDB audit delivery. It knows Application ports and Domain types, but not HTTP contracts. |
| Ambev.DeveloperEvaluation.IoC | Composition root for application, persistence, security, telemetry, and infrastructure registrations. It wires implementations to ports and contains no business rules. |
| Ambev.DeveloperEvaluation.Common | Shared security, validation, logging, and cross-cutting primitives. It must remain independent of feature workflows and domain orchestration. |
| Ambev.DeveloperEvaluation.WebApi | HTTP boundary for controllers, contracts, authentication, authorization, cookies, CORS, rate limiting, middleware, ETags, health, and hosting. It delegates use cases through MediatR. |

~~~mermaid
flowchart LR
    WebApi --> IoC
    IoC --> Application
    IoC --> ORM
    IoC --> Domain
    IoC --> Common
    ORM --> Application
    ORM --> Domain
    Application --> Domain
    Domain --> Common
~~~

This reflects the project references, including ORM's dependency on Application ports used by refresh sessions and outbox delivery.

## Frontend

src/frontend contains the Angular application, feature routes, API clients, authentication/session state, shared UI, Vitest tests, and Playwright scenarios. It consumes HTTP contracts and does not reproduce server-owned discount or concurrency rules.

## Tests

| Project | Purpose |
|---|---|
| Ambev.DeveloperEvaluation.Unit | Isolated domain, application, mapping, security, and infrastructure behavior without external services. |
| Ambev.DeveloperEvaluation.Integration | Real PostgreSQL and MongoDB behavior through Testcontainers, including persistence, concurrency, outbox, and audit idempotency. |
| Ambev.DeveloperEvaluation.Functional | ASP.NET Core pipeline, controllers, validation, authentication, authorization, errors, health, and HTTP contracts. |
| src/frontend/**/*.spec.ts | Angular components, guards, services, interceptors, forms, and UI behavior through Vitest and Angular TestBed. |
| src/frontend/e2e | Browser authentication, sales lifecycle, concurrency, resilience, accessibility, and telemetry through Playwright. |

## Request and event flow

~~~mermaid
flowchart LR
    Browser[Angular] --> WebApi[ASP.NET Core WebApi]
    WebApi --> MediatR
    MediatR --> Handler[Application handler]
    Handler --> Sale[Domain Sale aggregate]
    Handler --> UoW[Unit of Work]
    UoW --> PostgreSQL[(PostgreSQL)]
    UoW --> Outbox[(Transactional outbox)]
    Outbox --> Worker[Background outbox processor]
    Worker --> Audit[Idempotent MongoDB audit projection]
~~~

Aggregate changes and outbox messages are committed in one PostgreSQL transaction. The processor writes audit documents keyed by event ID and then marks successful delivery. MongoDB is not used to calculate or serve authoritative sale totals.
