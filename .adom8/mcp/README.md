# MCP Bootstrap (ADOm8)

This repository was bootstrapped by the ADOm8 onboarding pipeline with MCP guidance artifacts.

## What the pipeline configured automatically

- Created this guidance folder under `.adom8/mcp/`
- Added a starter MCP manifest at `.adom8/mcp/mcp.template.json`

## GitHub Copilot Coding Agent quick setup

1. Open GitHub repository settings → Copilot → Coding agent → MCP configuration.
2. Create GitHub environment `copilot` and add environment secret `COPILOT_MCP_AZURE_DEVOPS_PAT`.
3. Copy the contents of `.adom8/mcp/mcp.template.json` into that panel.
4. Save and run a coding session.

The generated template is schema-valid for GitHub Copilot Coding Agent MCP config.

## ADO MCP authentication

- The generated `ado` MCP server is configured with `--authentication envvar`.
- It maps `ADO_MCP_AUTH_TOKEN` to `COPILOT_MCP_AZURE_DEVOPS_PAT`.
- This pipeline can also sync a repository secret with the same name when `COPILOT_MCP_AZURE_DEVOPS_PAT` is provided as a pipeline secret variable.

## Coding Agent network policy (if firewall is enabled)

Allowlist the Azure DevOps domains used by MCP tool calls:

- `dev.azure.com`
- `vssps.dev.azure.com`
- `vsrm.dev.azure.com`

## Phase 1 ADOm8 stage bridge endpoints

Phase 1 stage updates are exposed as Function-key secured REST endpoints:

- `set-stage` → `https://adom8-func-751787bd.azurewebsites.net/api/mcp/set-stage?code=<FUNCTION_KEY>`
- `add-comment` → `https://adom8-func-751787bd.azurewebsites.net/api/mcp/add-comment?code=<FUNCTION_KEY>`
- `stage-event` → `https://adom8-func-751787bd.azurewebsites.net/api/mcp/stage-event?code=<FUNCTION_KEY>`

These are currently REST bridge endpoints (not native MCP protocol servers).
Use them directly from Copilot instructions/tooling when you need deterministic ADO updates.

## What cannot be automated in Azure DevOps pipeline

- Installing MCP client tooling on each developer machine
- Signing in interactively to provider accounts (GitHub, Azure DevOps) inside local MCP clients
- Approving organization-level policies that require admin UI confirmation

## Recommended next step

Use `mcp.template.json` as a starting point in your MCP client and provide credentials through the client's secure secret store.
