[Back to README](../README.md)

# Development setup

The API requires a JWT signing key with at least 32 bytes. Keep this key outside committed configuration.

## Rider or `dotnet run`

Configure the signing key in .NET User Secrets:

```powershell
dotnet user-secrets set "Jwt:SecretKey" "replace-with-a-random-secret-containing-at-least-32-characters" `
  --project src/backend/Ambev.DeveloperEvaluation.WebApi
```

Apply migrations and start the API:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update `
  --project src/backend/Ambev.DeveloperEvaluation.ORM `
  --startup-project src/backend/Ambev.DeveloperEvaluation.WebApi
dotnet run --project src/backend/Ambev.DeveloperEvaluation.WebApi
```

The Rider launch profile uses `http://localhost:5119`. PostgreSQL from Docker Compose is exposed on host port `5434`.

## Optional development administrator

The development-only seed is disabled by default and never runs in other environments. Enable it through User Secrets when a local administrator is needed:

```powershell
dotnet user-secrets set "DevelopmentAdmin:Enabled" "true" --project src/backend/Ambev.DeveloperEvaluation.WebApi
dotnet user-secrets set "DevelopmentAdmin:Email" "admin@example.com" --project src/backend/Ambev.DeveloperEvaluation.WebApi
dotnet user-secrets set "DevelopmentAdmin:Password" "choose-a-strong-local-password" --project src/backend/Ambev.DeveloperEvaluation.WebApi
```

The seed is idempotent by normalized email. It applies pending migrations and creates the administrator only when the configured email does not exist.

## Docker Compose

Copy `.env.example` to `.env`, replace the example signing key, and optionally configure the development administrator. Then run:

```powershell
docker compose up --detach --build
```

Production must supply `Jwt__SecretKey`, `Jwt__Issuer`, and `Jwt__Audience` from its secret/configuration provider. The application fails during startup when JWT configuration is missing or invalid.
