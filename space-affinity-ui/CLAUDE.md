# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with the UI in this folder. Repo-wide context (routing, images, deployment) is in [../CLAUDE.md](../CLAUDE.md).

## Commands

Run from this folder.

```bash
npm run dev        # http://localhost:5173, proxies /api to the API on :5247
npm run build      # tsc -b && vite build; this is the type check
npm run lint       # oxlint
```

There is no test runner.

## Architecture

The whole UI is [App.tsx](src/App.tsx): no router, no state library, no API client layer. It calls `fetch('/api/...')` with relative URLs and uses React 19 form actions (`<form action={fn}>` receiving `FormData`).

- The TypeScript types at the top of `App.tsx` are hand-written mirrors of the API's C# types. JSON is camelCase on the wire.
- The login session (`token`, `expiresAt`) lives in `sessionStorage`. A 401 on a write clears it and reopens the login form.
- `tsconfig.app.json` sets `erasableSyntaxOnly` and `verbatimModuleSyntax`: no enums or parameter properties, and type-only imports need `import type`.
