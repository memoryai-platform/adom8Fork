# Project Isolation Checklist (Shared Azure DevOps Organization)

Use this checklist when running multiple ADOm8 projects in the same Azure DevOps organization.

Goal: prevent one project's onboarding/provisioning run from changing process fields, states, or webhook behavior used by another project.

---

## 1) Process Isolation (Most Important)

- Create a dedicated **inherited process** per project.
  - Example: `ADOm8-Core-Process`, `ADOm8-CreditPlan-Process`
- Assign each Azure DevOps project to its own inherited process.
- Do not share the same inherited process between active test and production-like projects.

Why: process-level customizations (states/fields/rules/layout) are the highest-risk source of cross-project drift.

---

## 2) Service Hook Isolation

- Ensure each project has its own service-hook subscription.
- Validate each subscription has:
  - `eventType = workitem.updated`
  - `changedFields` includes `System.State`
  - `projectId` set to the correct project
  - target URL points to the matching Function App:
    - `https://<project-specific-function-app>.azurewebsites.net/api/webhook?code=<function-key>`
- Remove stale or duplicate subscriptions that point to old apps/routes.

Why: wrong target URL or stale hooks can silently route events to the wrong backend.

---

## 3) Provisioning Safety Rules

- Run provisioning only against the intended target project.
- Never run both projects' provisioning pipelines simultaneously in the same org.
- After a successful setup, treat provisioning as controlled change (not routine rerun).
- Before rerunning provisioning, capture a baseline snapshot:
  - process used by project
  - service-hook list + target URLs
  - key custom field definitions and defaults

---

## 4) Trigger Validation Before UAT

Before moving a story to `AI Agent`, verify:

- Smoke harness passes on the target app:

```powershell
pwsh ./scripts/function-smoke/run-function-smoke.ps1 \
  -FunctionAppUrl "https://<function-app>.azurewebsites.net" \
  -FunctionKey "<function-key>"
```

- ADO service hook test delivery returns HTTP 200 to `/api/webhook`.
- Queue and dead-letter counts are healthy.

---

## 5) Runtime Monitoring During State Move

When moving story state to `AI Agent`, confirm this chain in order:

1. ADO service-hook delivery is recorded.
2. `OrchestratorWebhook` invocation appears.
3. `agent-tasks` queue message appears.
4. `AgentTaskDispatcher` invocation appears.
5. On hard error, work item moves to `Agent Failed` and gets a comment.

If step 1 fails, issue is ADO config.
If step 1 passes but step 2 fails, issue is webhook target/key/routing.
If step 2 passes but step 4 fails, issue is queue/dispatcher/runtime.

---

## 6) Change Control for Shared Org

- Use a short change log whenever process/service-hook settings are modified.
- Keep one "known-good" baseline per project (process name, hook IDs, endpoint URL).
- If regression occurs, restore from baseline first, then debug code.

---

## 7) Recommended Team Convention

- One project = one inherited process = one Function App = one smoke test target.
- Test project changes first, production-like project second.
- Promote only after smoke harness + manual AI Agent trigger checklist pass.
