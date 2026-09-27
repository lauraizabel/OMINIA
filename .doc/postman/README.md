# Postman reviewer workflow

Import both files into Postman:

1. `DeveloperStore-Sales.postman_collection.json`
2. `DeveloperStore-Local.postman_environment.json`

Select the **DeveloperStore — Local** environment. Set `adminPassword` locally to the same value configured in `.env`; keep its variable type as `secret`. The default `baseUrl` is `http://localhost:5119`, and the default email is `admin@example.test`.

Start the application before running the collection:

This command is the same in Windows PowerShell, Linux, and macOS:

```shell
docker compose up --detach --build --wait --wait-timeout 240
```

Run the collection in order. Its folders are designed as a readable review sequence:

| Folder                                  | Purpose                                                                          |
| --------------------------------------- | -------------------------------------------------------------------------------- |
| `00 — Health and readiness`             | Verify liveness, PostgreSQL readiness, and diagnostic health output.                         |
| `01 — Authentication`                   | Log in, capture the JWT, rotate the cookie-backed session, and reject an untrusted origin.   |
| `02 — Complete sales lifecycle`         | Create, read, list, update, cancel, soft-delete, and verify uniqueness/not-found behavior.   |
| `03 — Optimistic concurrency`           | Exercise valid, malformed, and stale `If-Match` values.                                      |
| `04 — Validation and security examples` | Exercise domain, query, content-type, precondition, and authentication failures.             |
| `05 — Outbox operations`                | Read administrator-only operational metadata and safely test replay of an unknown event.     |
| `06 — Session termination`              | Revoke the refresh family, clear the cookie, and prove refresh/protected access are rejected. |

The scripts manage all runtime state:

- the Postman cookie jar retains the opaque `HttpOnly` refresh cookie; it is never copied into a variable;
- `accessToken` is read from `data.token` after login and refresh, then inherited as bearer authentication;
- login and refresh assert that the response body does not expose the refresh token;
- unique sale numbers are generated before creation;
- sale and item IDs are captured from responses;
- strong ETags are captured and sent through `If-Match`;
- the concurrency scenario preserves one stale ETag while tracking the current ETag;
- successful lifecycle and concurrency fixtures are soft-deleted by their cleanup requests.
- logout removes `accessToken` from collection state before the final anonymous checks.

The collection keeps failure injection deterministic. Rate-limit exhaustion, oversized 413 payloads, unavailable dependencies, and manufacturing a dead-letter record are covered by automated tests rather than by a reviewer flow that would alter or destabilize the local environment.

The exported collection and environment contain no password or token. Avoid exporting the environment after entering local credentials unless current values are removed first.

## Command-line execution

Newman can run the same workflow. Store the local password in a temporary shell environment variable so it is not added to the command itself.

Windows PowerShell:

```powershell
$env:POSTMAN_ADMIN_PASSWORD = '<local-development-password>'
npx --yes newman@6.2.1 run .doc/postman/DeveloperStore-Sales.postman_collection.json `
  --environment .doc/postman/DeveloperStore-Local.postman_environment.json `
  --env-var "adminPassword=$env:POSTMAN_ADMIN_PASSWORD" `
  --reporters cli
Remove-Item Env:POSTMAN_ADMIN_PASSWORD
```

Linux or macOS (bash/zsh):

```bash
export POSTMAN_ADMIN_PASSWORD='<local-development-password>'
npx --yes newman@6.2.1 run .doc/postman/DeveloperStore-Sales.postman_collection.json \
  --environment .doc/postman/DeveloperStore-Local.postman_environment.json \
  --env-var "adminPassword=$POSTMAN_ADMIN_PASSWORD" \
  --reporters cli
unset POSTMAN_ADMIN_PASSWORD
```

If only one protected folder is run, execute **01 — Authentication** first unless a valid `accessToken` and refresh cookie are already present. Always leave **06 — Session termination** until the end because it intentionally revokes the current refresh family.
