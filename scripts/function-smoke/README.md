# Function Smoke Harness

This script validates core Azure Function HTTP contracts with fast, deterministic checks.

## What it tests

- `GET /api/health` returns `200` with a valid status payload.
- `GET /api/status` returns `200` with expected shape.
- `POST /api/webhook` contracts:
  - empty body => `400` + `error: Empty request body`
  - invalid JSON => `400` + `error: Invalid JSON payload`
  - non-trigger state payload => `200` + `status: skipped`
- Optional triggering-state test (`AI Agent`) to confirm queued/validation response.
- `POST /api/copilot-webhook` contracts:
  - unsigned/empty request => `400` or `401` guardrail rejection
  - optional strict signed request => `200` (when webhook secret is provided)

## Usage

```powershell
pwsh ./scripts/function-smoke/run-function-smoke.ps1 `
  -FunctionAppUrl "https://<your-func-app>.azurewebsites.net" `
  -FunctionKey "<function-key>"
```

Optional strict GitHub signature validation:

```powershell
pwsh ./scripts/function-smoke/run-function-smoke.ps1 `
  -FunctionAppUrl "https://<your-func-app>.azurewebsites.net" `
  -FunctionKey "<function-key>" `
  -GitHubWebhookSecret "<github-webhook-secret>"
```

Optional queueing test (can enqueue an agent task):

```powershell
pwsh ./scripts/function-smoke/run-function-smoke.ps1 `
  -FunctionAppUrl "https://<your-func-app>.azurewebsites.net" `
  -FunctionKey "<function-key>" `
  -IncludeQueueingTests
```

## Payload fixtures

- `payloads/ado_non_trigger.json`
- `payloads/ado_ai_agent_trigger.json`
- `payloads/github_pr_ignored.json`

Use these as templates to mimic Azure DevOps and GitHub webhook bodies.
