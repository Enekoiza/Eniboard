# Eniboard — Frontend

Personal Kanban board for tracking pending changes across your own apps. React 19 + TypeScript + Vite + Tailwind CSS v4 + React Router v7 + TanStack Query.

## Running locally

```bash
npm install
cp .env.example .env   # then set VITE_API_BASE_URL to your backend API
npm run dev
```

The app runs at `http://localhost:5173` by default.

## Building

```bash
npm run build   # type-checks with tsc -b, then builds with Vite into dist/
npm run preview # serve the production build locally
```

## Environment variables

| Variable               | Description                                      |
|-------------------------|--------------------------------------------------|
| `VITE_API_BASE_URL`     | Base URL of the Eniboard backend API, no trailing slash (e.g. `https://api.eniboard.dev`). |

## Deploying

This is a static Vite SPA — deploy the `frontend/` directory to Vercel as-is; `vercel.json` includes the SPA rewrite so client-side routes resolve correctly. Set `VITE_API_BASE_URL` as a Vercel project environment variable.
