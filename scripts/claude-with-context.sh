#!/bin/bash
# =============================================================================
# claude-with-context.sh
# Helper script to run Claude Code CLI with .agent/ documentation context.
#
# Automatically loads codebase documentation so Claude Code generates code
# that matches your team's established patterns and conventions.
#
# Usage:
#   ./scripts/claude-with-context.sh "task description"
#
# Example:
#   ./scripts/claude-with-context.sh "add OAuth2 authentication to the API"
#
# Prerequisites:
#   - Claude Code CLI installed and in PATH
#   - .agent/ folder populated (run CodebaseDocumentationAgent first)
# =============================================================================

set -euo pipefail

# --- Argument validation ---

if [ -z "${1:-}" ]; then
    echo "Usage: ./scripts/claude-with-context.sh 'task description'"
    echo ""
    echo "Examples:"
    echo "  ./scripts/claude-with-context.sh 'add OAuth2 authentication'"
    echo "  ./scripts/claude-with-context.sh 'refactor StoryContext to support multi-region'"
    echo "  ./scripts/claude-with-context.sh 'add rate limiting to AI client'"
    echo ""
    echo "This script automatically includes .agent/ documentation context"
    echo "so Claude Code generates code matching your codebase patterns."
    exit 1
fi

TASK="$1"

# --- Prerequisite checks ---

# Check for .agent/ folder
if [ ! -d ".agent" ]; then
    echo "Error: .agent/ folder not found in current directory."
    echo ""
    echo "The .agent/ folder contains AI-optimized documentation about your codebase."
    echo "To generate it:"
    echo "  1. Open the AI Agents dashboard"
    echo "  2. Trigger the CodebaseDocumentationAgent"
    echo "  3. Wait for it to complete and push .agent/ files"
    echo "  4. Pull the latest changes: git pull"
    echo ""
    echo "Alternatively, create the folder manually:"
    echo "  mkdir -p .agent"
    echo "  # Add CONTEXT_INDEX.md, CODING_STANDARDS.md, etc."
    exit 1
fi

# Check for Claude Code CLI
if ! command -v claude &> /dev/null; then
    echo "Warning: 'claude' CLI not found in PATH."
    echo ""
    echo "Install Claude Code CLI:"
    echo "  npm install -g @anthropic-ai/claude-code"
    echo ""
    echo "Building prompt anyway (you can copy/paste it)..."
    echo ""
    CLAUDE_AVAILABLE=false
else
    CLAUDE_AVAILABLE=true
fi

# --- Load context files ---

echo "Loading context from .agent/ folder..."

CONTEXT=""
FILES_LOADED=0

# Always load CONTEXT_INDEX.md (master overview)
if [ -f ".agent/CONTEXT_INDEX.md" ]; then
    CONTEXT+="=== CONTEXT_INDEX.md ===
"
    CONTEXT+="$(cat .agent/CONTEXT_INDEX.md)"
    CONTEXT+="

"
    FILES_LOADED=$((FILES_LOADED + 1))
    echo "  ✓ Loaded CONTEXT_INDEX.md"
fi

# Always load CODING_STANDARDS.md (conventions)
if [ -f ".agent/CODING_STANDARDS.md" ]; then
    CONTEXT+="=== CODING_STANDARDS.md ===
"
    CONTEXT+="$(cat .agent/CODING_STANDARDS.md)"
    CONTEXT+="

"
    FILES_LOADED=$((FILES_LOADED + 1))
    echo "  ✓ Loaded CODING_STANDARDS.md"
fi

# Load ARCHITECTURE.md if present
if [ -f ".agent/ARCHITECTURE.md" ]; then
    CONTEXT+="=== ARCHITECTURE.md ===
"
    CONTEXT+="$(cat .agent/ARCHITECTURE.md)"
    CONTEXT+="

"
    FILES_LOADED=$((FILES_LOADED + 1))
    echo "  ✓ Loaded ARCHITECTURE.md"
fi

# Load COMMON_PATTERNS.md if present
if [ -f ".agent/COMMON_PATTERNS.md" ]; then
    CONTEXT+="=== COMMON_PATTERNS.md ===
"
    CONTEXT+="$(cat .agent/COMMON_PATTERNS.md)"
    CONTEXT+="

"
    FILES_LOADED=$((FILES_LOADED + 1))
    echo "  ✓ Loaded COMMON_PATTERNS.md"
fi

# Load all feature files
if [ -d ".agent/FEATURES" ]; then
    for feature_file in .agent/FEATURES/*.md; do
        if [ -f "$feature_file" ]; then
            FEATURE_NAME=$(basename "$feature_file")
            CONTEXT+="=== FEATURES/$FEATURE_NAME ===
"
            CONTEXT+="$(cat "$feature_file")"
            CONTEXT+="

"
            FILES_LOADED=$((FILES_LOADED + 1))
            echo "  ✓ Loaded FEATURES/$FEATURE_NAME"
        fi
    done
fi

if [ "$FILES_LOADED" -eq 0 ]; then
    echo ""
    echo "Warning: No documentation files found in .agent/ folder."
    echo "Run CodebaseDocumentationAgent to generate documentation."
    echo "Proceeding without context..."
    echo ""
fi

echo ""
echo "Loaded $FILES_LOADED context files."
echo ""

# --- Build prompt ---

PROMPT="Task: $TASK

Context from codebase (.agent/ documentation):

$CONTEXT

Requirements:
- Follow all patterns and conventions documented above
- Match existing code style and architecture
- Use established interfaces and patterns (I{Feature} interface pattern)
- Use constructor dependency injection (register in Program.cs)
- Add XML comments on all public methods and classes
- Document decisions in .ado/stories/US-{workItemId}/DECISIONS.md
- Add comprehensive tests (80%+ coverage) using xUnit + Moq
- Use structured logging with ILogger<T>
- Follow naming: PascalCase for types, _camelCase for fields, Method_Scenario_Result for tests
- Include CancellationToken on all async methods
- Use sealed classes where inheritance isn't needed
- Link code to user story in comments where applicable

Proceed with implementation following the documented patterns."

# --- Execute ---

if [ "$CLAUDE_AVAILABLE" = true ]; then
    echo "Calling Claude Code with context..."
    echo "---"
    echo ""
    claude "$PROMPT"
else
    echo "===== GENERATED PROMPT (copy/paste into your AI tool) ====="
    echo ""
    echo "$PROMPT"
    echo ""
    echo "===== END PROMPT ====="
    exit 0
fi
