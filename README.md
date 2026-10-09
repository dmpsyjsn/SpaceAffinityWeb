# Space Affinity Web

A small "space notes" app (a description plus a picture URL) built mainly as a **learning project for Docker and Kubernetes deployment**. The app is deliberately simple. The interesting parts are the container images, the Kubernetes manifests and the CI/CD pipeline that builds them on ARM and deploys them to a single-node cluster.

## Architecture

```
                 ┌──────────────── Kubernetes cluster ────────────────┐
                 │                                                     │
 browser ──► Traefik ingress ──/api──► web-api (x2) ──► postgres (StatefulSet + PVC)
                 │          └──/─────► web-ui  (x2)                     │
                 └─────────────────────────────────────────────────────┘
```

| Component | Tech | Image |
|-----------|------|-------|
| **API** ([SpaceAffinityApi/](SpaceAffinityApi/)) | .NET 10 minimal API, Native AOT, raw SQL via Npgsql | Chiseled `runtime-deps` (no shell, non-root) |
| **UI** ([space-affinity-ui/](space-affinity-ui/)) | React 19 + TypeScript + Vite | `nginx-unprivileged` serving the static build |
| **Database** | PostgreSQL 17 | Official `postgres:17` |

The UI calls `/api/...` on its own origin. In development the Vite dev server proxies that to the API. In the cluster the Traefik ingress routes it.

## Repository layout

```
SpaceAffinityApi/            .NET API + Dockerfile
space-affinity-ui/           React UI + Dockerfile + nginx.conf
k8s/
  postgres.yaml              Headless Service + StatefulSet with a 5Gi volume
  api.yaml                   Deployment (2 replicas, probes on /healthz) + Service
  ui.yaml                    Deployment (2 replicas) + Service
  ingress-local.yaml         Traefik Ingress: /api → API, / → UI
.github/
  workflows/build-and-deploy.yml   Build on ARM, smoke test, push to GHCR, deploy over SSH
  scripts/smoke-test.sh            Runs the API image against a throwaway Postgres
```

## API

| Method | Path | Notes |
|--------|------|-------|
| `GET` | `/healthz` | Used by the Kubernetes readiness and liveness probes |
| `GET` | `/api/spacenotes?page=1&itemsPerPage=100` | Paging values are clamped (`itemsPerPage` is 1 to 100) |
| `POST` | `/api/spacenotes` | Body `{ "description": "...", "pictureUrl": "..." }` adds a note (201). Include `"id"` to edit that note instead (200, or 404 if it doesn't exist). Returns 400 if a field is missing |

The API creates its table on startup. It takes a Postgres advisory lock first, so several pods starting at the same time don't race on `CREATE TABLE`.

## Running locally

### 1. Without Kubernetes (fastest feedback loop)

Prerequisites: .NET 10 SDK, Node 24, Docker.

```bash
# Postgres (matches appsettings.Development.json)
docker run -d --name spaceaffinity-pg -p 5432:5432 \
  -e POSTGRES_USER=app -e POSTGRES_PASSWORD=devpassword -e POSTGRES_DB=appdb \
  postgres:17

# API on http://localhost:5247
dotnet run --project SpaceAffinityApi --launch-profile http

# UI on http://localhost:5173 (proxies /api to the API)
cd space-affinity-ui
npm install
npm run dev
```

The development connection string uses `Host=host.docker.internal`, which works with Docker Desktop. On other setups, change it to `localhost`.

### 2. Building the images

The API's build context is the **repo root**. The UI's build context is its own folder.

```bash
docker build -f SpaceAffinityApi/Dockerfile -t space-affinity-api:1.0 .
docker build -t space-affinity-ui:1.0 space-affinity-ui
```

Native AOT compiles for the architecture of the machine doing the build. It isn't cross-compiled, so an image built on an x64 PC is amd64-only.

To smoke test the API image the same way CI does:

```bash
bash .github/scripts/smoke-test.sh space-affinity-api:1.0
```

### 3. On a local Kubernetes cluster

This works with any local cluster that uses Traefik as its ingress controller and can see your locally built images, such as k3s, k3d or Rancher Desktop. The manifests reference `space-affinity-api:1.0` and `space-affinity-ui:1.0`, so build those first.

```bash
# The database password lives in a Secret that is not committed
kubectl create secret generic postgres-credentials --from-literal=password='<choose-one>'

kubectl apply -f k8s/postgres.yaml -f k8s/api.yaml -f k8s/ui.yaml -f k8s/ingress-local.yaml
kubectl rollout status deployment/web-api
kubectl get pods
```

Then open the cluster's ingress address, for example `http://localhost`.

## CI/CD

[build-and-deploy.yml](.github/workflows/build-and-deploy.yml) runs on every push to `main`:

1. **Build** on GitHub's free `ubuntu-24.04-arm` runner. The deploy target is an ARM (Oracle Cloud A1) server, and Native AOT has to compile natively for that architecture.
2. **Smoke test** the API image against a real Postgres container. The test calls actual endpoints because Native AOT problems (trimming, reflection) often only appear the first time a code path runs, not at startup.
3. **Push** both images to GHCR, tagged with the commit SHA and `latest`.
4. **Deploy** (only when the repo variable `DEPLOY_ENABLED` is `true`):
   - Rewrite the `image:` lines in the manifests to point at this commit's SHA-tagged images
   - Pipe the manifests over SSH to `kubectl apply` on the server
   - Wait for both rollouts, and run `kubectl rollout undo` if either fails

A `concurrency` group makes sure two deploys never run at the same time.

### Server setup the workflow assumes

- A Kubernetes cluster (k3s, for example) where the `ubuntu` user can run `kubectl`
- The `postgres-credentials` Secret already created, as shown above
- An Ingress for the public host. `ingress-local.yaml` isn't applied by the pipeline.
- GHCR images the cluster can pull. The manifests have no `imagePullSecrets`, so the packages need to be public.

Repository secrets: `DEPLOY_HOST`, `DEPLOY_SSH_KEY`, `DEPLOY_KNOWN_HOSTS`. Repository variable: `DEPLOY_ENABLED`.

## Things this project was used to learn

- **Multi-stage Dockerfiles** that keep build tooling out of the runtime image, ending in minimal non-root images (chiseled .NET, unprivileged nginx)
- **Native AOT** for a small, fast-starting API, and the catch that it compiles natively, which is why CI builds on ARM
- **Deployments vs StatefulSets**: stateless API and UI replicas next to a single Postgres pod with a persistent volume claim
- **Secrets and env var expansion**: the connection string is built with `$(DB_PASSWORD)` from a `secretKeyRef`
- **Readiness and liveness probes**, so rolling updates only send traffic to pods that are actually ready
- **Safe concurrent startup** with a Postgres advisory lock around schema creation
- **Path-based ingress routing**, so the UI and API share one origin with no CORS setup
- **Immutable SHA image tags, rollout status checks and automatic rollback** in the pipeline

## License

[MIT](LICENSE)
