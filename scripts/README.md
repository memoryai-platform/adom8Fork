# Developer Scripts

Helper scripts for working with AI agents and maintaining code consistency.

## claude-with-context.sh

Wrapper for Claude Code CLI that automatically includes `.agent/` documentation context in prompts.

### Usage

```bash
./scripts/claude-with-context.sh "task description"
```

### Examples

```bash
# Add a new feature
./scripts/claude-with-context.sh "add OAuth2 support to authentication"

# Refactor existing code
./scripts/claude-with-context.sh "refactor StoryContext to support multi-region storage"

# Add tests
./scripts/claude-with-context.sh "add integration tests for DeploymentAgentService"

# Fix a bug
./scripts/claude-with-context.sh "fix race condition in concurrent state file access"
```

### What It Does

1. Validates `.agent/` folder exists
2. Checks Claude Code CLI is installed
3. Loads context files automatically:
   - `.agent/CONTEXT_INDEX.md` — master overview (always loaded)
   - `.agent/CODING_STANDARDS.md` — conventions and patterns (always loaded)
   - `.agent/ARCHITECTURE.md` — system design (if present)
   - `.agent/COMMON_PATTERNS.md` — how-to patterns (if present)
   - `.agent/FEATURES/*.md` — all feature documentation (if present)
4. Builds a structured prompt with your task + loaded context
5. Calls Claude Code CLI with the complete prompt

### Prerequisites

- **Claude Code CLI** installed and in PATH
  ```bash
  npm install -g @anthropic-ai/claude-code
  ```
- **`.agent/` folder populated** — run CodebaseDocumentationAgent first via the dashboard, or create manually
- Run from the **repository root** (where `.agent/` folder is located)

### Fallback Mode

If Claude Code CLI is not installed, the script prints the generated prompt so you can copy/paste it into any AI tool (Cursor, GitHub Copilot Chat, ChatGPT, etc.).

### Why Use This

| Without Context | With Context |
|----------------|--------------|
| Generic code patterns | Code matching YOUR codebase conventions |
| Random naming | Consistent naming (`I{Feature}`, `{Name}AgentService`) |
| Mixed DI approaches | Constructor injection, `Program.cs` registration |
| Mixed test frameworks | xUnit + Moq, `Method_Scenario_Result` naming |
| Missing cancellation tokens | `CancellationToken` on all async methods |

**Result:** Consistent code whether written by AI agents, humans, or humans with AI tools.

## Adding New Scripts

When adding developer helper scripts:

1. Place them in the `scripts/` directory
2. Include a shebang line (`#!/bin/bash`)
3. Add `set -euo pipefail` for safety
4. Include usage instructions when called without arguments
5. Validate prerequisites before executing
6. Document the script in this README
7. Make executable: `chmod +x scripts/your-script.sh`
