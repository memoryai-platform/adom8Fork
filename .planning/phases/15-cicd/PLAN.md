# Phase 15: CI/CD Workflows

**Goal:** GitHub Actions workflows for deployment

## Files to Create

1. `.github/workflows/deploy-functions.yml`
   - Trigger: push to main, paths src/AIAgents.*/**
   - Steps: checkout, setup .NET 8, restore, build, publish, deploy
   - Uses azure/functions-action@v1
   - Secrets: AZURE_FUNCTIONAPP_NAME, AZURE_FUNCTIONAPP_PUBLISH_PROFILE

2. `.github/workflows/deploy-dashboard.yml`
   - Trigger: push to main, paths dashboard/**
   - Steps: checkout, deploy to Static Web App
   - Uses Azure/static-web-apps-deploy@v1
   - Secret: AZURE_STATIC_WEB_APPS_API_TOKEN
