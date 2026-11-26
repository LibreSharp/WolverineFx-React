# Client

React + TypeScript + Vite frontend for this WolverineFx + React app.

## Development

```bash
npm install
npm run dev             # http://localhost:5173, proxies /api/* to the API (see vite.config.ts)
```

The API must be running separately (`dotnet run` from `src/Api`) for the proxy and `gen:api` to
have something to talk to.

## Scripts

| Script | Effect |
|---|---|
| `npm run dev` | Start the Vite dev server |
| `npm run build` | Type-check (`tsc -b`) and build for production |
| `npm run preview` | Preview the production build locally |
| `npm run lint` | Run Oxlint |
| `npm run gen:api` | Regenerate `src/lib/api/schema.d.ts` from the API's live OpenAPI spec (requires the API running on `localhost:5133`) |

## API client

`src/lib/api/schema.d.ts` is generated, not hand-written — after changing an API contract in
`src/Api`, run the API and then `npm run gen:api` to regenerate the typed client
(`openapi-fetch` + `openapi-typescript`).
