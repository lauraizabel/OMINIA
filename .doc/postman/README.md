# Postman reviewer workflow

Import both files into Postman:

1. `DeveloperStore-Sales.postman_collection.json`
2. `DeveloperStore-Local.postman_environment.json`

Select the **DeveloperStore — Local** environment. Set `adminPassword` locally to the same value configured in `.env`; keep its variable type as `secret`. The default `baseUrl` is `http://localhost:5119`, and the default email is `admin@example.test`.

Start the application before running the collection:

```powershell
docker compose up --detach --build --wait --wait-timeout 240
```

Run the collection in order. Its folders are designed as a readable review sequence:

| Folder                                  | Purpose                                                                          |
| --------------------------------------- | -------------------------------------------------------------------------------- |
| `00 — Health and readiness`             | Verify process and PostgreSQL health.                                            |
| `01 — Authentication`                   | Log in and capture the JWT automatically.                                        |
| `02 — Complete sales lifecycle`         | Create, read, list, update, cancel, soft-delete, and verify absence.             |
| `03 — Optimistic concurrency`           | Prove that a stale ETag returns `412 Precondition Failed`.                       |
| `04 — Validation and security examples` | Exercise quantity, duplicate-product, precondition, and authentication failures. |

The scripts manage all runtime state:

- `accessToken` is read from `data.token` after login and inherited as bearer authentication;
- unique sale numbers are generated before creation;
- sale and item IDs are captured from responses;
- strong ETags are captured and sent through `If-Match`;
- the concurrency scenario preserves one stale ETag while tracking the current ETag;
- successful lifecycle and concurrency fixtures are soft-deleted by their cleanup requests.

The exported workflow was validated against a fresh disposable Compose stack on September 25, 2026: 20 requests and 36 assertions completed with zero failures.

The exported collection and environment contain no password or token. Avoid exporting the environment after entering local credentials unless current values are removed first.

## Command-line execution

Newman can run the same workflow. Store the local password in a temporary shell environment variable so it is not added to the command itself:

```powershell
$env:POSTMAN_ADMIN_PASSWORD = '<local-development-password>'
npx --yes newman@6.2.1 run .doc/postman/DeveloperStore-Sales.postman_collection.json `
  --environment .doc/postman/DeveloperStore-Local.postman_environment.json `
  --env-var "adminPassword=$env:POSTMAN_ADMIN_PASSWORD" `
  --reporters cli
Remove-Item Env:POSTMAN_ADMIN_PASSWORD
```

If only one folder is run, execute **01 — Authentication** first unless a valid `accessToken` is already present in the collection runtime.
