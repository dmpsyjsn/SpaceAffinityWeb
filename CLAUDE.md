# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A small "space notes" app (description + picture URL) that exists mainly as a learning project for Docker and Kubernetes deployment. The app code is deliberately simple; the container images, manifests and pipeline are the point. Keep changes proportionate to that. [README.md](README.md) has the full deployment walkthrough.

Two deployables plus a database, all served from one origin (no CORS anywhere):

- [SpaceAffinityApi/](SpaceAffinityApi/): .NET 10 minimal API, published as **Native AOT**, raw SQL over Npgsql
- [space-affinity-ui/](space-affinity-ui/): React 19 + TypeScript + Vite, served by nginx in the cluster
- PostgreSQL 17

`/api/*` reaches the API and everything else reaches the UI. Three things implement that same routing and have to agree: the Vite dev proxy ([vite.config.ts](space-affinity-ui/vite.config.ts)), the Traefik ingress ([k8s/ingress.yaml](k8s/ingress.yaml), [k8s/ingress-local.yaml](k8s/ingress-local.yaml)), and the `/api` route group in [Program.cs](SpaceAffinityApi/Program.cs).

Each project has its own CLAUDE.md with its commands and architecture: [SpaceAffinityApi/CLAUDE.md](SpaceAffinityApi/CLAUDE.md) and [space-affinity-ui/CLAUDE.md](space-affinity-ui/CLAUDE.md). This file covers only what spans both.

## Images and the smoke test

Run from the repo root.

```bash
# The API build context is the repo root, the UI's is its own folder.
docker build -f SpaceAffinityApi/Dockerfile -t space-affinity-api:1.0 .
docker build -t space-affinity-ui:1.0 space-affinity-ui

# The only automated test: runs the API image against a throwaway Postgres
bash .github/scripts/smoke-test.sh space-affinity-api:1.0
```

There are no unit test projects and no JS test runner. [smoke-test.sh](.github/scripts/smoke-test.sh) is the whole test suite, and it tests the built image, so it needs Docker and a prior `docker build`. It has to supply every setting the API requires and match the API's endpoint contracts, so API changes often need a matching change there and in [k8s/api.yaml](k8s/api.yaml).

Native AOT is not cross-compiled: the API image matches the architecture of the machine that built it. CI builds on an ARM runner because the server is ARM.

## Deployment

[build-and-deploy.yml](.github/workflows/build-and-deploy.yml) runs on **every push to `main`**: build both images on ARM, smoke test the API image, push to GHCR, then (if the repo variable `DEPLOY_ENABLED` is `true`) apply the manifests over SSH and roll back on a failed rollout. A push to `main` is a production deploy to `spaceaffinity.win`.

- The deploy step rewrites image tags with `sed`, matching the literal lines `image: space-affinity-api:...` and `image: space-affinity-ui:...`. Renaming those images in the manifests silently breaks the substitution.
- The pipeline applies only `postgres.yaml`, `api.yaml` and `ui.yaml`. `ingress.yaml`, `cluster-issuer.yaml` and `redirect-https.yaml` (TLS via cert-manager, HTTPS redirect) are applied to the server by hand, as is the `postgres-credentials` Secret. `ingress-local.yaml` is for local clusters only.
- Both containers run as non-root and listen on 8080. The API image is chiseled (no shell), so `kubectl exec` debugging is not available.
