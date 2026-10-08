#!/usr/bin/env bash
# Smoke-tests an API image against a throwaway Postgres: startup, reads, writes and validation.
# Calls real endpoints because Native AOT problems tend to appear on first use of a code path, not at startup.
# Usage: bash .github/scripts/smoke-test.sh <api-image>
set -euo pipefail

IMAGE="$1"
NAME="smoke-$$"
PORT="${SMOKE_PORT:-18080}"
BASE="http://localhost:$PORT"

cleanup() {
  docker rm -f "$NAME-api" "$NAME-pg" >/dev/null 2>&1 || true
  docker network rm "$NAME" >/dev/null 2>&1 || true
}
trap cleanup EXIT

fail() {
  echo "SMOKE TEST FAILED: $1"
  echo "---- API logs"
  docker logs "$NAME-api" 2>&1 | tail -50 || true
  exit 1
}

docker network create "$NAME" >/dev/null
docker run -d --name "$NAME-pg" --network "$NAME" \
  -e POSTGRES_USER=app -e POSTGRES_PASSWORD=smoke -e POSTGRES_DB=appdb \
  postgres:17 >/dev/null

# -h localhost checks over TCP, which only opens once Postgres has fully initialized
for _ in $(seq 1 60); do
  docker exec "$NAME-pg" pg_isready -h localhost -U app -d appdb >/dev/null 2>&1 && break
  sleep 1
done

docker run -d --name "$NAME-api" --network "$NAME" -p "$PORT:8080" \
  -e ConnectionStrings__Default="Host=$NAME-pg;Database=appdb;Username=app;Password=smoke" \
  "$IMAGE" >/dev/null

for _ in $(seq 1 30); do
  curl -fs "$BASE/healthz" >/dev/null 2>&1 && break
  sleep 1
done
curl -fs "$BASE/healthz" >/dev/null || fail "API never became healthy"
echo "ok   GET  /healthz"

[ "$(curl -fs "$BASE/api/spacenotes" | tr -d '[:space:]')" = "[]" ] || fail "GET on an empty table should return []"
echo "ok   GET  /api/spacenotes (empty)"

created=$(curl -fs -X POST -H "Content-Type: application/json" \
  -d '{"description":"smoke test","pictureUrl":"https://example.com/smoke.jpg"}' \
  "$BASE/api/spacenotes") || fail "POST returned an error status"
echo "$created" | tr -d '[:space:]' | grep -q '"id":1' || fail "POST should return the generated id 1, got: $created"
echo "ok   POST /api/spacenotes (returns id)"

curl -fs "$BASE/api/spacenotes" | grep -q "smoke test" || fail "GET should return the note just created"
echo "ok   GET  /api/spacenotes (reads back)"

curl -fs "$BASE/api/spacenotes?page=0&itemsPerPage=-5" >/dev/null || fail "out-of-range paging should be clamped, not error"
echo "ok   GET  /api/spacenotes?page=0 (clamped)"

status=$(curl -s -o /dev/null -w "%{http_code}" -X POST -H "Content-Type: application/json" -d '{}' "$BASE/api/spacenotes")
[ "$status" = "400" ] || fail "POST without fields should return 400, got $status"
echo "ok   POST /api/spacenotes {} (400)"

echo "Smoke test passed for $IMAGE"
