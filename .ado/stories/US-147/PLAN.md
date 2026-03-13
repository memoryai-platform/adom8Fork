# Planning Analysis for US-147

## Story Overview

**ID:** US-147  
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
This story requires updating the React dashboard's visual branding from purple/violet to Azure DevOps blue theme and replacing the current logo with the legacy ADOm8 logo. The work involves CSS color changes, logo asset replacement, and end-to-end validation that the integration pipeline still functions correctly after the branding update.

### Recommended Approach
1. Update CSS variables and theme colors in dashboard/index.html from purple/violet to Azure DevOps blue (#0078D4 and related shades). 2. Replace current logo references with the specified SVG asset (logo-option-chunky-infinity-box.svg). 3. Ensure logo displays with transparent background. 4. Apply changes consistently across all UI components (header, sidebar, navigation, badges, panels). 5. Test the complete integration flow by creating a test story in Azure DevOps and verifying it appears in the dashboard. The logo asset already exists and uses Azure DevOps blue gradients, making this primarily a CSS update task.

### Affected Files

- `dashboard/index.html`

- `dashboard/public/brand/logo-option-chunky-infinity-box.svg`


### Complexity Estimate
**Story Points:** 5

### Architecture Considerations
Single-file dashboard modification. The dashboard is a vanilla JS/HTML/CSS SPA in dashboard/index.html (~1850+ lines). All styling is embedded in <style> tags within the file. Logo assets are referenced from the public/brand/ directory. No build step required - changes are immediately deployable to Azure Static Web Apps.

---

## Implementation Plan

### Sub-Tasks

1. Identify all purple/violet color references in dashboard CSS

2. Replace purple/violet colors with Azure DevOps blue theme colors

3. Update logo references to use logo-option-chunky-infinity-box.svg

4. Verify logo displays with transparent background

5. Apply branding consistently across header, sidebar, navigation, badges, and panels

6. Test dashboard functionality after branding changes

7. Create test Azure DevOps story to validate end-to-end integration

8. Verify test story appears correctly in updated dashboard


### Dependencies


- Existing logo asset at dashboard/public/brand/logo-option-chunky-infinity-box.svg

- Azure DevOps integration pipeline must be functional

- Dashboard deployment to Azure Static Web Apps



---

## Risk Assessment

### Identified Risks

- CSS changes might affect dashboard functionality or layout

- Logo asset path might need adjustment based on deployment structure

- Color changes might impact accessibility or readability

- Integration test might reveal issues with the pipeline


---

## Assumptions Made

- The logo asset logo-option-chunky-infinity-box.svg exists and is properly formatted

- Azure DevOps integration is currently working

- Dashboard is deployed and accessible

- Current purple/violet branding is easily identifiable in the CSS


---

## Testing Strategy
1. Visual testing: Compare before/after screenshots of all dashboard sections. 2. Functional testing: Verify all dashboard features work after color changes (refresh, filters, expand/collapse, etc.). 3. Integration testing: Create a new Azure DevOps user story in the 'ADO - ai agents azure' project and verify it flows through the pipeline and appears in the dashboard. 4. Cross-browser testing: Verify branding appears correctly in different browsers. 5. Accessibility testing: Ensure color contrast ratios meet accessibility standards.

---

*Generated by Planning Agent*  
*Timestamp: 2026-03-13T14:28:42.8220209Z*
