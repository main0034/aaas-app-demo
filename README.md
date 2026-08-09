# aaas-app-demo

Demo application for the AaaS POC. A copy of `aaas-app-template`, used to prove the image handoff (Phase 4b) before any code generation is involved.

Deploys to `deployments/dev/demo` in `aaas-deployments`.

## Endpoints

- `GET /health` — liveness/readiness, never touches the database
- `GET /ready` — reports database connectivity
- `GET /items`, `POST /items` — Postgres-backed, proves the connection works end to end

## Local development

```bash
python -m venv .venv && source .venv/bin/activate
pip install -r requirements-dev.txt
pytest -q
uvicorn app.main:app --reload
```

## Proving the handoff

Change something trivial and observable — the `APP_NAME` default, or add a field to the `/health` response. Then:

1. Open a PR. `ci.yml` lints, tests, builds, and smoke-tests the container.
2. Merge. `release.yml` pushes `ghcr.io/main0034/aaas-app-demo:<sha>` and opens a PR against `aaas-deployments`.
3. That PR's plan should show exactly one change: `container_image`.
4. Merge it. `apply.yml` rolls a new revision and comments the URL.

If step 3 shows more than one changed attribute, something is wrong — investigate before merging.

## Agents

Read `AGENT.md` first.
