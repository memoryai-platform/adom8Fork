# Self-Healing Dataverse Monitor

## What This Is

A self-healing system that monitors Dataverse plugin trace logs for recurring errors, classifies them using a code-first/AI-last triage funnel, and automatically creates Bug work items in Azure DevOps that flow through the existing AI agent pipeline (Planning → Coding → Testing → Review). The pipeline stops at Code Review so a human always approves the fix before it merges. This is a new feature added to the existing ADO-Agent (ADOm8) platform.

## Core Value

Production bugs in Dataverse plugins are automatically detected, triaged, and fixed with zero human intervention until Code Review — closing the feedback loop between production errors and code fixes.

## Requirements

### Validated

- ✓ Azure Functions pipeline with queue-based agent dispatch — existing
- ✓ Work item creation via AzureDevOpsClient.CreateWorkItemAsync — existing
- ✓ AI classification via IAIClient.CompleteAsync — existing
- ✓ Table Storage activity logging — existing
- ✓ Timer-triggered functions (DeadLetterQueueHandler, CopilotTimeoutChecker) — existing
- ✓ Feature-flagged optional integrations (Copilot, SaaS, MCP) — existing
- ✓ Agent pipeline: Planning → Coding → Testing → Review → Documentation → Deployment — existing
- ✓ Service hook webhook for work item state changes — existing
- ✓ Custom work item states (AI Agent, Code Review, etc.) for User Story — existing
- ✓ Error categorization (Transient, Configuration, Data, Code) — existing
- ✓ Resilient HTTP clients with circuit breaker — existing

### Active

- [ ] Dataverse Web API client with OAuth2 client credentials authentication
- [ ] PluginTraceLog entity querying with OData filters
- [ ] Error message normalization (strip GUIDs, timestamps, IDs for consistent hashing)
- [ ] ErrorTracking table in Azure Table Storage for deduplication and classification caching
- [ ] 5-layer triage funnel: config gate → Dataverse query → dedup lookup → rule-based filter → AI classification
- [ ] AI-powered error classification (CRITICAL/BUG/MONITOR/NOISE) with cached results
- [ ] Bug work item creation (parameterize CreateWorkItemAsync for Bug type)
- [ ] Bug work item state provisioning (same AI states as User Story)
- [ ] Timer-triggered PluginTraceLogMonitor with configurable cron schedule
- [ ] Scan watermark persistence in Table Storage
- [ ] Per-plugin occurrence threshold configuration
- [ ] Resolved detection (errors that stop appearing → mark resolved)
- [ ] Regression detection (resolved errors that reappear → new Bug)
- [ ] HTTP endpoint for manual error suppression/unsuppression
- [ ] HTTP endpoint to view tracked errors (dashboard support)
- [ ] Feature-flag deployment (disabled by default, enabled via Dataverse config keys)

### Out of Scope

- Real-time streaming from Dataverse (polling is sufficient for error detection cadence) — complexity vs. value
- Automatic merge without human review — self-healing must stop at Code Review for safety
- Dataverse plugin deployment (the system fixes code, not plugin registration) — different pipeline
- Multi-environment monitoring (one Function App monitors one Dataverse environment) — v2
- Dashboard UI for error tracking (API endpoint only for now) — v2
- Custom Workflow/Power Automate integration — different trigger mechanism

## Context

- **Existing platform:** ADOm8 — an AI agent pipeline that takes User Stories through Planning → Coding → Testing → Review → Documentation → Deployment
- **Tech stack:** .NET 8, Azure Functions Worker SDK, C# 12, Azure Table Storage, Azure Queue Storage
- **AI providers:** Claude (Anthropic), OpenAI, Azure OpenAI — multi-provider via AIClientFactory
- **Repository providers:** GitHub or Azure DevOps Repos (provider-agnostic via IRepositoryProvider)
- **No existing Dataverse integration** — this is entirely new. Requires MSAL for OAuth2 client credentials and Dataverse Web API calls.
- **Feature flag pattern established:** Copilot integration is the template — `CopilotOptions.Enabled` + config keys, dormant until configured
- **Work item creation currently hardcoded to User Story type** — needs parameterization for Bug support
- **PluginTraceLog entity** in Dataverse contains: typename (plugin class), messageblock (error message), exceptiondetails (stack trace), createdon (timestamp)

## Constraints

- **Tech stack**: Must be .NET 8 / Azure Functions — matches existing codebase
- **Auth**: Azure AD App Registration with client credentials flow — required for unattended Dataverse access
- **AI cost**: Code-first triage funnel must minimize AI calls — AI is last resort, results are cached permanently
- **Safety**: All auto-detected bugs must stop at Code Review state — human approval required before merge
- **Backward compat**: CreateWorkItemAsync must default to "User Story" — existing callers must not break
- **No new infrastructure**: Feature flag in existing Function App — zero new Azure resources beyond config

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Feature flag in existing app (not separate Function App) | Follows Copilot/SaaS/MCP pattern; zero new infra; client just adds config keys | — Pending |
| Bug work items (not User Stories) | Semantically correct for defects; requires parameterizing CreateWorkItemAsync and provisioning Bug states | — Pending |
| Configurable per-plugin thresholds | Different plugins have different noise levels; one threshold doesn't fit all | — Pending |
| Code-first, AI-last triage funnel | AI calls are expensive; most runs should never call AI; cache classifications permanently | — Pending |
| Table Storage watermark (not fixed lookback) | Survives restarts; consistent with existing patterns; no missed or duplicate scans | — Pending |
| App Registration auth (not managed identity) | More portable across environments; standard for Dataverse server-to-server | — Pending |
| HTTP suppression endpoint | Allows humans to mute known issues without code changes | — Pending |
| Configurable cron schedule (not hardcoded) | Different environments need different scan frequencies | — Pending |

---
*Last updated: 2026-03-14 after initialization*
