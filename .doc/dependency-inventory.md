[Back to README](../README.md)

## Direct dependency inventory

Package metadata was read from NuGet on September 23, 2026. Transitive packages remain governed by their own package metadata and lock resolution.

| Package | Version | Declared license |
|---|---:|---|
| AutoMapper | 13.0.1 | MIT |
| BCrypt.Net-Next | 4.0.3 | License file |
| Bogus | 35.6.1 | License URL |
| coverlet.collector | 6.0.2 | MIT |
| coverlet.msbuild | 6.0.2 | MIT |
| FluentAssertions | 6.12.0 | Apache-2.0 |
| FluentValidation | 11.10.0 | Apache-2.0 |
| MediatR | 12.4.1 | Apache-2.0 |
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.10 | MIT |
| Microsoft.EntityFrameworkCore | 8.0.10 | MIT |
| Microsoft.EntityFrameworkCore.Design | 8.0.10 | MIT |
| Microsoft.EntityFrameworkCore.Relational | 8.0.10 | MIT |
| Microsoft.Extensions.Caching.Memory | 6.0.2 | MIT |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.2 | MIT |
| Microsoft.Extensions.Diagnostics.HealthChecks | 8.0.10 | MIT |
| Microsoft.NET.Test.Sdk | 17.11.1 | MIT |
| Microsoft.VisualStudio.Azure.Containers.Tools.Targets | 1.20.1 | Package EULA file |
| Npgsql.EntityFrameworkCore.PostgreSQL | 8.0.8 | PostgreSQL |
| NSubstitute | 5.1.0 | BSD-3-Clause |
| OneOf | 3.0.271 | License URL |
| Roslynator.Analyzers | 4.12.4 | Apache-2.0 |
| Roslynator.Testing.CSharp.Xunit | 4.12.4 | Apache-2.0 |
| Serilog.AspNetCore | 8.0.3 | Apache-2.0 |
| Serilog.Enrichers.Environment | 3.0.1 | Apache-2.0 |
| Serilog.Enrichers.ExceptionData | 1.0.0 | Apache-2.0 |
| Serilog.Exceptions | 8.4.0 | MIT |
| Serilog.Exceptions.EntityFrameworkCore | 8.4.0 | MIT |
| Serilog.Expressions | 5.0.0 | Apache-2.0 |
| Serilog.Sinks.Console | 6.0.0 | Apache-2.0 |
| Swashbuckle.AspNetCore | 6.8.1 | MIT |
| xunit | 2.9.2 | Apache-2.0 |
| xunit.runner.visualstudio | 2.8.2 | Apache-2.0 |

### Vulnerability review

`dotnet list package --vulnerable --include-transitive` reports one remaining advisory: AutoMapper 13.0.1, described in the baseline risk register. The vulnerable transitive `Microsoft.Extensions.Caching.Memory 6.0.0` introduced by `Serilog.Exceptions.EntityFrameworkCore` was overridden with patched version 6.0.2.

