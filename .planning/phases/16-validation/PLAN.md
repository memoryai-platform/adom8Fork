# Phase 16: Build Validation

**Goal:** Verify everything compiles and validates

## Checks

1. `dotnet build src/AIAgents.sln` — zero errors, zero warnings
2. `terraform init` + `terraform validate` in infrastructure/ — passes
3. All interfaces have implementations
4. No orphan stub methods
5. Queue pipeline: Webhook → queue → Dispatcher → keyed IAgentService
6. State machine: Story Planning → AI Code → AI Test → AI Review → AI Docs → Ready for QA
7. Dashboard loads, polls API, bugs visible (progress=0%, completed=blue)
8. local.settings.json has all required config keys
9. Activity log writes at agent lifecycle points

## Fix Any Issues

If build fails, fix the errors and re-validate.
