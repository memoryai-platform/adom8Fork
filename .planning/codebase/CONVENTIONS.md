# Coding Conventions

**Analysis Date:** 2026-03-14

## Naming Patterns

**Files:**
- C# files: PascalCase matching class/namespace name (e.g., `AIOptions.cs`, `PlanningAgentService.cs`)
- React components: PascalCase with `.jsx` extension (e.g., `AppHeader.jsx`, `MetricCard.jsx`)
- Utilities/helpers: camelCase with `.js` or `.cs` extension (e.g., `api.js`, `formatting.js`)
- Test files: `[ClassName]Tests.cs` or `[Component].test.js`
- Configuration files: Snake_case or PascalCase based on purpose (e.g., `tailwind.config.js`, `vite.config.js`)

**Functions:**
- C#: PascalCase for public methods (e.g., `CompleteAsync()`, `ResolveEffectiveOptions()`)
- C#: Private methods prefixed with underscore (e.g., `_logger`, `_httpClientFactory`)
- JavaScript: camelCase for functions and hooks (e.g., `getCurrentStatus()`, `useAgentStatus()`)
- React hooks: `use*` prefix for custom hooks (e.g., `useAgentStatus`, `useAppKey`, `useCodebaseIntelligence`)

**Variables:**
- C#: camelCase for local variables and parameters (e.g., `systemPrompt`, `userPrompt`, `effectiveOptions`)
- C#: PascalCase for properties (e.g., `Provider`, `Model`, `ApiKey`)
- C#: PascalCase or UPPER_SNAKE_CASE for constants (e.g., `s_options`, `ProcessingState`, `InitializeCodebaseTag`)
- JavaScript: camelCase for all variables (e.g., `appKey`, `lastUpdated`, `connectionStatus`)

**Types:**
- C# classes: PascalCase with meaningful suffixes: `*Options` for configuration, `*Service` for business logic, `*Factory` for object creation
- C# interfaces: `I*` prefix (e.g., `IAIClient`, `IStoryContext`, `ICodebaseContextProvider`)
- React components: Export default function with PascalCase name
- Type hints in JSDoc: `@param {{label: string, value: string}}` format

## Code Style

**Formatting:**
- C#: 4-space indentation, implicit using statements enabled
- JavaScript: 2-space indentation (inferred from Tailwind classes, component patterns)
- No explicit linter configured; code follows .NET/React community standards
- Imports organized with blank lines between groups

**Linting:**
- C#: Uses modern C# 11+ features (`required` keyword, `sealed` classes, record types)
- Nullable annotations enabled (`#nullable enable`)
- `ImplicitUsings` enabled for C# projects
- No explicit ESLint/Prettier config found in JavaScript; follows standard formatting

**Language Features (C#):**
- Init-only properties preferred: `public string Name { get; init; }`
- Sealed classes for concrete implementations: `public sealed class AIClient`
- Required properties for mandatory fields: `public required string Content { get; init; }`
- Pattern matching for conditionals
- Records for data structures where appropriate
- Global using statements for test projects

## Import Organization

**C# Order:**
1. System namespaces (e.g., `using System;`)
2. System.* extensions (e.g., `using System.Text.Json;`)
3. Microsoft.* namespaces (e.g., `using Microsoft.Extensions.Logging;`)
4. Third-party packages (e.g., `using Moq;`)
5. Project namespaces (e.g., `using AIAgents.Core.Models;`)

**JavaScript Order:**
1. React/standard library imports (e.g., `import { useState } from 'react'`)
2. Third-party packages (e.g., `import { formatDistanceToNowStrict } from 'date-fns'`)
3. Relative imports from project (e.g., `import { config } from '../config'`)
4. CSS/styles last (e.g., `import './style.css'`)

**Path Aliases:**
- No path aliases configured in current setup
- Relative paths used throughout (e.g., `../config`, `../api`)

## Error Handling

**Patterns:**
- C# services: Throw exceptions from interfaces; callers decide handling strategy
- C# tests: Use `Assert.*` for condition validation
- JavaScript: `try/catch` with specific error code checking (e.g., `if (error.code === 401)`)
- JavaScript: Attach error metadata to Error objects (e.g., `error.code`, `error.responseData`)
- Custom error classes: Extend Error in JavaScript, create exception classes in C#

**Example (C#):**
```csharp
if (!response.ok)
{
    var error = new HttpRequestException(...);
    throw error;
}
```

**Example (JavaScript):**
```javascript
if (response.status === 401 || response.status === 403) {
    const unauthorizedError = new Error('Unauthorized');
    unauthorizedError.code = response.status;
    throw unauthorizedError;
}
```

## Logging

**Framework:**
- C#: `Microsoft.Extensions.Logging.ILogger<T>`
- JavaScript: No logging framework; uses `console` implicitly (not found in current code)

**Patterns:**
- C#: Dependency-injected logger, used via `_logger.LogDebug()`, `_logger.LogError()`, etc.
- Log messages include context: `_logger.LogDebug("Sending completion request to {Provider} model {Model}", _options.Provider, _options.Model)`
- No verbose logging in happy path; debug level for details, error level for failures

## Comments

**When to Comment:**
- C# uses `/// <summary>` XML doc comments on all public types and methods (found throughout codebase)
- JSDoc comments on exported functions: `/** @param {{...}} props */` format
- No inline comments unless logic is complex or non-obvious
- Clear code is preferred over comments

**JSDoc/TSDoc:**
- C# fully embraces XML documentation with `<summary>`, `<param>`, `<returns>`, `<example>` tags
- JavaScript uses JSDoc sparingly for prop documentation and public exports
- Example from codebase: `/// <summary>Configuration options for the AI completion provider...</summary>`

**Example (C#):**
```csharp
/// <summary>
/// Thin AI completion client that handles HTTP transport to both
/// Anthropic (Claude Messages API) and OpenAI-compatible endpoints.
/// </summary>
public sealed class AIClient : IAIClient
{
    /// <summary>
    /// Sends a completion request to the configured AI provider.
    /// </summary>
    public async Task<AICompletionResult> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        AICompletionOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

**Example (JavaScript):**
```javascript
/**
 * @param {{label: string, value: string, detail?: string, helpText?: string}} props
 */
export default function MetricCard({ detail, helpText, label, value }) {
```

## Function Design

**Size:**
- C# methods: 30-50 lines typical, up to 100 for complex orchestration
- JavaScript: Functions generally 20-40 lines for hooks and utilities
- Extraction preferred over long functions

**Parameters:**
- C# public methods: Typed parameters with optional `CancellationToken` as final param
- C#: Use tuples or classes for multiple return values
- JavaScript: Destructured props for React components
- JavaScript: Options objects for multiple optional parameters

**Return Values:**
- C# services: Return typed objects (`Task<T>`), nullable when absent
- C# factory methods: Return interfaces to enable mocking/substitution
- JavaScript: Return objects with named properties for clarity (e.g., `{ connectionStatus, data, error, loading }`)
- Implicit conversion supported where appropriate (e.g., `AICompletionResult` to `string`)

## Module Design

**Exports:**
- C# public classes in interfaces file (e.g., `IAIClient.cs` contains interface + supporting classes)
- JavaScript: Default exports for components (`export default function Component()`)
- Named exports for utilities and hooks (`export function useAgentStatus()`, `export async function validateAppKey()`)
- Index files not used; direct imports from source

**Barrel Files:**
- Not used in current structure
- Each module imports directly from source files

**Organization Pattern:**
- C#: Namespace-based organization matching folder structure
- JavaScript: Component-based folders with related hooks/utils nearby

---

*Convention analysis: 2026-03-14*
