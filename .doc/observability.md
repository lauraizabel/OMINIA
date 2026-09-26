[Back to README](../README.md)

# Distributed tracing

The local delivery includes end-to-end OpenTelemetry tracing for the Angular client, ASP.NET Core API, PostgreSQL calls, outbound HTTP calls, and asynchronous outbox delivery. Jaeger receives OTLP data and provides a searchable trace UI.

## Run and inspect

Start the complete Compose stack, sign in, and exercise a sale flow:

```powershell
docker compose up --detach --build --wait --wait-timeout 240
```

Open `http://localhost:16686`, select either `ambev-sales-portal` or `ambev-sales-api`, and choose **Find Traces**. A browser API span and its server span share the same W3C trace ID. Sale events retain that trace ID when the outbox processor later writes the audit projection, which connects the user action to eventual delivery across process boundaries.

The API also returns the active trace ID as the error correlation ID. This gives support a stable lookup key without exposing exception details.

## Configuration

Tracing is disabled by default in committed application settings. Compose enables it explicitly:

| Component | Setting | Compose value |
|---|---|---|
| API | `Telemetry__Enabled` | `true` |
| API | `Telemetry__ServiceName` | `ambev-sales-api` (default) |
| API | `Telemetry__OtlpEndpoint` | `http://telemetry:4317` |
| Browser | `/telemetry-config.json` | mounted from `.config/frontend-telemetry.compose.json` |
| Browser | OTLP endpoint | same-origin `/otel/v1/traces` |

The browser exports through Nginx instead of contacting the collector across origins. This keeps the deployment boundary explicit and avoids a permissive collector CORS policy. The runtime JSON file lets an environment enable or disable telemetry without rebuilding Angular. The client rejects cross-origin exporter endpoints.

For `npm start`, the development proxy forwards `/otel` to `http://127.0.0.1:4318`. Copy the enabled values from `.config/frontend-telemetry.compose.json` to `src/frontend/public/telemetry-config.json` only when browser tracing is wanted during local development; do not commit that temporary change.

## Data and cardinality rules

Telemetry must remain operational metadata rather than a second audit store:

- browser spans contain the HTTP method, response status, and normalized URL path;
- sale IDs in route paths are replaced with `{id}`, and query strings are excluded;
- credentials, access tokens, refresh tokens, request or response bodies, email addresses, exception messages, and stack traces are never added as attributes;
- unhandled browser failures record only a sanitized error type and source;
- outbox spans contain technical message and aggregate identifiers, event type, and aggregate version;
- health probes are excluded from API traces to avoid noise.

Production should send OTLP to a managed collector with authentication, TLS, sampling, retention, and access controls appropriate to the environment. Jaeger in this repository is a reproducible local backend and is not a production deployment topology.

## Automated evidence

Angular unit tests verify safe attributes, URL normalization, propagation, status handling, and unhandled-error sanitization. Playwright scenario E13 captures the browser OTLP request, verifies a valid W3C `traceparent` on API calls, and confirms that credentials and authorization headers are absent from the exported payload. The Compose smoke job also sends telemetry through the Nginx proxy and waits until an API service trace is queryable through Jaeger.
