# Sales Portal frontend

Angular 22 frontend for the DeveloperStore sales API.

## Requirements

- Node.js >=22.22.3 <23
- npm >=10 <11
- API at http://localhost:5119 for local development

## Run locally

~~~powershell
npm ci
npm start
~~~

Open http://localhost:4200. The Angular development server proxies /api to the backend.

## Authentication flow

The access token is kept only in memory. Session continuity across page reloads is provided through a rotating refresh token stored in an `HttpOnly` cookie.

- The auth interceptor attaches the in-memory access token to API requests.
- Application initialization calls the refresh endpoint, so a valid cookie restores the session after reload.
- A 401 on an authenticated API request triggers one shared refresh request and retries the original request with the new access token.
- Logout revokes the server-side refresh family, clears the cookie, and removes the in-memory session.
- Route guards preserve a validated internal returnUrl and restore that destination after login.

The frontend does not write authentication tokens to localStorage, sessionStorage, or readable cookies.

## Quality checks

~~~powershell
npm run lint
npx tsc -p tsconfig.app.json --noEmit
npm run test:ci -- --coverage
npm run build
~~~

Angular TestBed runs through Vitest. CI enforces independent 90% line and branch coverage for the frontend.

## Playwright

~~~powershell
npm run test:e2e:install
npm run test:e2e
~~~

The default E2E command starts an isolated API and PostgreSQL environment with generated credentials. Use npm run test:e2e:existing to target an already running environment. See [e2e/README.md](e2e/README.md) for the scenario matrix.
