# Phase 2: Terraform Infrastructure

**Goal:** All Terraform files for Azure resources

## Files to Create

1. `infrastructure/main.tf` — Provider config, resource group
2. `infrastructure/variables.tf` — Input variables with validation
3. `infrastructure/storage.tf` — Storage account, queues, blob container, table
4. `infrastructure/functions.tf` — Consumption plan, App Insights, Function App
5. `infrastructure/static-web-app.tf` — Static Web App for dashboard
6. `infrastructure/outputs.tf` — URLs, connection strings, next-steps block
7. `infrastructure/terraform.tfvars.example` — Sample values

## Acceptance Criteria

- [ ] terraform init succeeds
- [ ] terraform validate passes
- [ ] All Azure resources defined (RG, Storage, Queues, Functions, Static Web App, App Insights)
- [ ] Lifecycle ignore on secret app_settings
- [ ] terraform.tfvars.example has sample values
