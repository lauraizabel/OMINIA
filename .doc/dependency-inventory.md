[Back to README](../README.md)

# Dependency and mapping decisions

Project files, package.json, package-lock.json, and Docker Compose are the version sources of truth. CI runs .config/audit-vulnerable-packages.ps1 against all .NET projects; the frontend lockfile provides the reproducible npm dependency graph.

## Backend dependency groups

| Function | Direct dependencies |
|---|---|
| Application flow | MediatR 12.4.1, FluentValidation 11.10.0, OneOf 3.0.271 |
| Mapping | Riok.Mapperly 4.3.1 |
| Relational persistence | EF Core 8.0.10, Npgsql EF Core 8.0.8 |
| Audit persistence | MongoDB.Driver 3.12.0 |
| Authentication and security | ASP.NET Core JwtBearer 8.0.10, BCrypt.Net-Next 4.0.3 |
| Logging and tracing | Serilog 8 packages, OpenTelemetry 1.16 packages |
| API discovery | Swashbuckle.AspNetCore 6.8.1 |
| Tests | xUnit 2.9.2, NSubstitute 5.1.0, FluentAssertions 6.12.0, Bogus 35.6.1, Testcontainers PostgreSQL/MongoDB 4.15.0, Coverlet 6.0.2 |

Microsoft.Extensions.Caching.Memory is explicitly pinned to 6.0.2 to override the vulnerable transitive 6.0.0 version introduced by Serilog.Exceptions.EntityFrameworkCore.

## Frontend dependency groups

Angular 22.2, RxJS 7.8, and TypeScript 6 implement the application. Vitest 5 and Angular TestBed provide unit/component tests, Playwright 1.55 provides E2E coverage, and angular-eslint/ESLint provide static checks. OpenTelemetry browser packages export sanitized traces through the same-origin frontend proxy.

## Why Mapperly replaced AutoMapper

AutoMapper 13.0.1 was removed after high-severity advisory [GHSA-rvv3-g6hj-g44x](https://github.com/advisories/GHSA-rvv3-g6hj-g44x). Fixed AutoMapper releases start in the newer commercial-license line. The repository therefore uses Apache-2.0-licensed Riok.Mapperly 4.3.1.

Mapperly generates feature-local mappings at compile time. Mapping failures surface during build, no runtime mapper service or reflection is required, and each feature keeps its mapping boundary explicit. The package uses PrivateAssets=all and ExcludeAssets=runtime because only its source generator and annotations are needed.

## Infrastructure images

Docker Compose pins PostgreSQL 16.15-alpine, MongoDB 8.0.15, and Jaeger 2.21.0. These images support local review; a production environment must define its own patching, registry, retention, TLS, and access-control policies.
