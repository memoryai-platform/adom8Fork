# Planning Analysis for US-149

## Story Overview

**ID:** US-149  
**Title:** Update React dashboard branding to Azure DevOps blue and restore legacy logo  
**State:** AI Agent  
**Created:** 2026-03-13

### Description
<div><span>As a user of the ADOm8 dashboard, I want the new React dashboard to use an Azure DevOps-style blue theme and the legacy ADOm8 logo so that the published dashboard matches the expected branding while validating the Azure DevOps, Azure Function, and dashboard integration end to end.<br></span> </div><div> </div><div><div><br> </div><div>Description<br> </div><div><br> </div><div>The new React dashboard is published and connected to the Azure Function for the ADO - ai agents azure project. Create this story to validate that a newly created Azure DevOps user story flows through the integration and appears correctly in the live dashboard.<br> </div><div><br> </div><div>As part of this work, update the dashboard branding by replacing the current purple/violet primary color with a blue similar to Azure DevOps, and replace the current logo/brand mark with the SVG logo.<br> </div><div><br> </div><div>logo asset to use:<br> </div><div>ADO-Agent\dashboard\public\brand\logo-option-chunky-infinity-box.svg<br> </div><div><br> </div><span>The logo should render with a transparent background if it does not already.</span><br> </div>

### Acceptance Criteria
<div><span>The primary accent color in the React dashboard is changed from purple/violet to a blue visually aligned with Azure DevOps branding.<br></span><div>The legacy logo is used in place of the current dashboard logo/mark.<br> </div><div>The logo asset used is ADO-Agent\dashboard\public\brand\logo-option-chunky-infinity-box.svg<br> </div><div>The logo displays with a transparent background.<br> </div><div>The updated branding is applied consistently across the main dashboard experience, including login, header, sidebar, key links, badges, and hero/status panels.<br> </div><div>Obvious purple/violet branding is removed from the main user-facing dashboard experience unless it is serving a non-brand semantic purpose.<br> </div><div>A user story created in the ADO - ai agents azure Azure DevOps project appears in the published dashboard through the existing integration.<br> </div><span>The live dashboard continues to function correctly after the branding update, including authentication, navigation, story visibility, and agent status display.</span><br> </div>

---

## Technical Analysis

### Problem Analysis
This story requires updating the React dashboard's visual branding from purple/violet to Azure DevOps blue theme and replacing the current logo with the legacy ADOm8 logo. The story also includes validating the end-to-end integration by creating a test user story in Azure DevOps and confirming it appears in the published dashboard. This is primarily a UI/branding update with integration validation.

### Recommended Approach
1. Locate and update CSS color variables/classes in the dashboard to replace purple/violet with Azure DevOps blue (#0078D4 and related shades). 2. Replace the current logo references with the specified SVG file (logo-option-chunky-infinity-box.svg). 3. Ensure the logo renders with transparent background. 4. Apply branding consistently across all dashboard components (header, sidebar, navigation, badges, panels). 5. Test the integration by creating a user story in the ADO project and verifying it flows through to the dashboard. The dashboard appears to be a single-file SPA (index.html) based on the codebase structure, making updates straightforward.

### Affected Files

- `dashboard/index.html`

- `dashboard/public/brand/logo-option-chunky-infinity-box.svg`


### Complexity Estimate
**Story Points:** 5

### Architecture Considerations
Single-file dashboard update with CSS color scheme changes and logo asset replacement. The dashboard is a vanilla JS SPA deployed to Azure Static Web Apps, so changes involve updating embedded CSS styles and image references within the HTML file.

---

## Implementation Plan

### Sub-Tasks

1. Identify all purple/violet color references in dashboard CSS

2. Replace purple/violet colors with Azure DevOps blue theme colors

3. Update logo references to use logo-option-chunky-infinity-box.svg

4. Verify logo displays with transparent background

5. Apply branding consistently across login, header, sidebar, badges, and panels

6. Remove obvious purple/violet branding from user-facing elements

7. Create test user story in ADO project to validate integration

8. Verify test story appears correctly in published dashboard

9. Test all dashboard functionality after branding update


### Dependencies


- Access to the published React dashboard

- Access to ADO - ai agents azure Azure DevOps project

- Existing Azure Function integration must be working

- Logo asset must exist at specified path



---

## Risk Assessment

### Identified Risks

- CSS changes might affect dashboard functionality or layout

- Logo asset might not exist at the specified path

- Integration test might reveal issues with the ADO-Function-Dashboard pipeline

- Color changes might reduce accessibility or readability


---

## Assumptions Made

- The logo asset exists at the specified path and has transparent background

- The dashboard is the single-file SPA mentioned in the codebase documentation

- Azure DevOps integration is currently functional

- The published dashboard is accessible and operational


---

## Testing Strategy
1. Visual testing: Compare before/after screenshots of all dashboard sections to ensure consistent blue branding. 2. Logo testing: Verify the new logo displays correctly with transparent background across different browser contexts. 3. Integration testing: Create a test user story in the ADO project, transition it through states, and verify it appears correctly in the dashboard. 4. Functional testing: Ensure all dashboard features (authentication, navigation, story visibility, agent status display) continue working after branding changes. 5. Cross-browser testing: Verify branding appears correctly in major browsers. 6. Accessibility testing: Ensure color contrast meets accessibility standards.

---

*Generated by Planning Agent*  
*Timestamp: 2026-03-13T23:09:47.8713286Z*
