# Phase 8: Scriban Templates

**Goal:** All markdown templates in Scriban syntax

## Files to Create (in src/AIAgents.Core/Templates/ AND .ado/templates/)

1. `PLAN.template.md` — Planning analysis output
2. `CODE_REVIEW.template.md` — Code review output with score-based sections
3. `TASKS.template.md` — Task breakdown
4. `CONVERSATION.template.md` — Agent conversation log
5. `DECISIONS.template.md` — Architecture decision records
6. `TEST_PLAN.template.md` — Test plan
7. `DOCUMENTATION.template.md` — Generated documentation
8. `state.schema.json` — JSON schema for state.json

## Syntax Notes

Templates use Scriban syntax (NOT mustache):
- Variables: `{{ WORK_ITEM_ID }}`
- Loops: `{{ for item in ITEMS }}...{{ end }}`
- Conditionals: `{{ if SCORE >= 90 }}...{{ else }}...{{ end }}`
- Item access: `{{ item.PROPERTY }}`
- Array size: `{{ ITEMS | array.size }}`

Use ScriptObject with UPPERCASE keys so simple variables match spec intent.
