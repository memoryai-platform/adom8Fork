# React Dashboard Branding Update

## Project

`ADO - ai agents azure`

## Title

Update React dashboard branding to Azure DevOps blue and restore the legacy logo

## User Story

As a user of the ADOm8 dashboard, I want the new React dashboard to use an Azure DevOps-style blue theme and the legacy ADOm8 logo so that the published dashboard matches the expected branding while validating the Azure DevOps, Azure Function, and dashboard integration end to end.

## Description

The new React dashboard is published and connected to the Azure Function for the `ADO - ai agents azure` project. Create this story to validate that a newly created Azure DevOps user story flows through the integration and appears correctly in the live dashboard.

As part of this work, update the dashboard branding by replacing the current purple and violet primary color usage with a blue similar to Azure DevOps. Replace the current dashboard logo and brand mark with the legacy SVG logo now stored in this repo.

The branding change must be deployed through the existing dashboard publish workflow so the live Static Web App reflects the update, rather than leaving the change only in local source.

Repo-local logo asset:

`C:\ADO-Agent\ADO-Agent\dashboard\public\brand\logo-option-chunky-infinity-box.svg`

Expected deployed asset path:

`/brand/logo-option-chunky-infinity-box.svg`

The logo should render with a transparent background if it does not already.

## Acceptance Criteria

1. The primary accent color in the React dashboard is changed from purple and violet to a blue visually aligned with Azure DevOps branding.
2. The legacy logo is used in place of the current dashboard logo and brand mark.
3. The logo asset used is `C:\ADO-Agent\ADO-Agent\dashboard\public\brand\logo-option-chunky-infinity-box.svg`.
4. The logo displays with a transparent background.
5. The updated branding is applied consistently across the main dashboard experience, including login, header, sidebar, key links, badges, and hero or status panels.
6. Obvious purple and violet branding is removed from the main user-facing dashboard experience unless it is intentionally retained for a non-brand semantic purpose.
7. A user story created in the `ADO - ai agents azure` Azure DevOps project appears in the published dashboard through the existing integration.
8. The branding change is published through the existing dashboard deployment workflow at `.github/workflows/deploy-dashboard.yml`, and the live Static Web App reflects the update.
9. The live dashboard continues to function correctly after the branding update, including authentication, navigation, story visibility, and agent status display.

## Test Notes

Create the story in Azure DevOps under `ADO - ai agents azure`, confirm it is picked up by the integration, and verify the published React dashboard reflects both the new work item and the updated branding after the dashboard publish workflow completes.
