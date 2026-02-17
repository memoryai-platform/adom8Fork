# Copilot Coding Agent Instructions

## Repository Overview

This is a .NET 8 / Azure Functions project with a single-file SPA dashboard.

## Dashboard

The dashboard is at `dashboard/index.html` — a single-file vanilla JS/HTML/CSS SPA (~1800 lines).
All CSS styles, JavaScript logic, and HTML markup are in this one file.

## Build & Test

```bash
dotnet build src/AIAgents.sln
dotnet test src/AIAgents.sln
```

## Key Rules

- **Make the actual code change** — do not just add documentation or plans
- The dashboard is `dashboard/index.html` — edit it directly for any CSS/JS/HTML changes
- Do NOT modify test files, CI/CD workflows, or infrastructure (Terraform)
- Match existing code style and conventions
- Ensure correct syntax, imports, and compilation
