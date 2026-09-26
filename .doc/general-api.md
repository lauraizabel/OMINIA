[Back to README](../README.md)

# General API conventions

The [Sales API contract](sales-api.md) is authoritative for sales-specific fields, filters, ETags, and status codes. Swagger and the committed OpenAPI document expose the complete HTTP surface.

## Authentication and authorization

Sales endpoints require a bearer JWT and the Manager or Admin role. User administration and outbox operations require Admin. Login, refresh, and logout are anonymous by design because refresh/logout use the HttpOnly cookie rather than requiring a valid bearer token; they retain origin validation and rate limits.

## Pagination and ordering

GET /api/sales uses _page and _size, with defaults of 1 and 10 and a maximum size of 100. _order accepts only the documented sales fields and always adds id as a stable tie-breaker. Invalid or unknown parameters return 400 rather than being ignored.

## Concurrency

Mutations of an existing sale require one strong ETag in If-Match. A missing header returns 428, malformed input returns 400, and a stale token returns 412.

## Error format

API errors use application/problem+json and do not expose stack traces.

~~~json
{
  "type": "ValidationError",
  "error": "Invalid input data",
  "detail": "Correct the fields listed in errors.",
  "traceId": "request-correlation-id",
  "errors": [
    {
      "field": "items[0].quantity",
      "code": "QuantityOutOfRange",
      "message": "Quantity must be between 1 and 20."
    }
  ]
}
~~~

The errors collection is present when field-level details are available. Authentication, authorization, not-found, conflict, concurrency, request-size, rate-limit, and dependency failures use the corresponding HTTP status documented in [sales-api.md](sales-api.md#error-contract).
