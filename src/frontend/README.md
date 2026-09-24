# Sales Portal frontend

Angular frontend for the sales API. The application keeps the JWT only in memory, so reloading the page requires signing in again.

## Requirements

- Node.js 22.22.3 or later in the Node 22 release line
- npm 10
- API running at `http://localhost:5119`

## Run locally

```powershell
npm ci
npm start
```

Open `http://localhost:4200`. The Angular development server proxies `/api` to the local backend.

## Validate

```powershell
npm run test:ci
npm run build
```

The application uses strict TypeScript compilation and Vitest through Angular TestBed.
