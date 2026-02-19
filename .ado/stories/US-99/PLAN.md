# Planning Analysis for US-99

## Story Overview

**ID:** US-99  
**Title:** Move Lock and Codebase Initialized controls next to Provision ADO  
**State:** Story Planning  
**Created:** 2026-02-19

### Description
<div>As an operator, I want the Lock button and Codebase Initialized control next to Provision ADO on the Dashboard so controls are visible, grouped, and not blocking actions. </div><div><br> </div><div><b>Repository/UI implementation context (verified):</b> </div><div>- Frontend is a single-file SPA at <code>dashboard/index.html</code>. </div><div>- Provision ADO control is currently rendered in the top nav as button id <code>provision-btn</code>. </div><div>- Lock control is currently rendered in header tools as button id <code>function-key-btn</code>. </div><div>- Codebase status control is currently rendered in header tools as span id <code>codebase-badge</code> with scan button id <code>btn-scan-codebase</code>. </div><div>- Current behavior hooks: <code>handleProvisionAdo()</code>, <code>openFunctionKeyPrompt()</code>, <code>openScanModal()</code>/<code>confirmCodebaseScan()</code>. </div><div><br> </div><div><b>Current layout baseline:</b> </div><div>- <code>provision-btn</code> is in the <code>.top-nav</code> section. </div><div>- <code>function-key-btn</code> and <code>codebase-badge</code> are in <code>.header-tools</code> inside the main header container. </div><div>- This story moves Lock and Codebase controls to sit next to Provision ADO in the same top-nav controls group, preserving existing behavior and IDs. </div><div><br> </div><div>Supporting visual references for implementation: </div><div>- Screenshot 2026-02-19 021100.jpg (AttachedFile) </div><div>- image.png (pasted image in description) </div>

### Acceptance Criteria
<div><b>Acceptance Criteria</b> </div><ul><li>Frontend file to modify is <code>dashboard/index.html</code> (single-file SPA). </li><li>Move controls so top-nav order is exactly: Provision ADO (<code>provision-btn</code>) then Lock (<code>function-key-btn</code>) then Codebase Initialized (<code>codebase-badge</code>). </li><li>Preserve existing IDs and click handlers: <code>provision-btn</code>/<code>handleProvisionAdo()</code>, <code>function-key-btn</code>/<code>openFunctionKeyPrompt()</code>, <code>codebase-badge</code> + <code>btn-scan-codebase</code>/<code>openScanModal()</code>. </li><li>Desktop breakpoint: at 1024px width and above, all three controls are visible on one row, no overlap, all click targets fully usable. </li><li>Tablet/mobile breakpoint: at widths below 1024px, wrapping is allowed, but order remains Provision ADO then Lock then Codebase Initialized, with no click-target overlap. </li><li>Story header render rule: show <code>US-99: Move Lock and Codebase Initialized controls next to Provision ADO</code> when title exists; never <code>US-99: US-99</code>. </li><li>Implementation and validation must use supporting files: Screenshot 2026-02-19 021100.jpg and image.png. </li> </ul>

---

## Technical Analysis

### Problem Analysis
The story requires moving two UI controls (Lock button and Codebase Initialized control) from their current location in the header tools section to sit next to the Provision ADO button in the top navigation. This is a pure frontend layout change affecting only the single-file SPA at dashboard/index.html. The current layout has provision-btn in .top-nav, while function-key-btn and codebase-badge are in .header-tools. The goal is to group all three controls together in the top-nav for better visibility and organization.

### Recommended Approach
This is a straightforward DOM restructuring task in the single-file SPA. The implementation involves: 1) Locating the existing HTML elements by their IDs (provision-btn, function-key-btn, codebase-badge), 2) Moving function-key-btn and codebase-badge from .header-tools to .top-nav section, 3) Ensuring the order is exactly: Provision ADO, Lock, Codebase Initialized, 4) Preserving all existing IDs and event handlers, 5) Adding responsive CSS to handle desktop (1024px+) single-row layout and mobile wrapping behavior, 6) Testing click targets remain fully functional at all breakpoints. No JavaScript logic changes are required - only HTML structure and CSS styling modifications.

### Affected Files

- `dashboard/index.html`


### Complexity Estimate
**Story Points:** 3

### Architecture Considerations
Single-file SPA modification affecting only the presentation layer. No backend services, APIs, or data models are involved. The change is purely structural HTML/CSS with no functional logic modifications required.

---

## Implementation Plan

### Sub-Tasks

1. Analyze current HTML structure to locate provision-btn in .top-nav and function-key-btn/codebase-badge in .header-tools

2. Move function-key-btn and codebase-badge HTML elements from .header-tools to .top-nav section

3. Reorder elements in .top-nav to achieve: provision-btn, function-key-btn, codebase-badge sequence

4. Verify all existing IDs are preserved: provision-btn, function-key-btn, codebase-badge, btn-scan-codebase

5. Verify all existing click handlers remain intact: handleProvisionAdo(), openFunctionKeyPrompt(), openScanModal()

6. Add/modify CSS for desktop breakpoint (1024px+): ensure all three controls visible on one row without overlap

7. Add/modify CSS for mobile breakpoint (<1024px): allow wrapping while maintaining order and preventing click-target overlap

8. Test responsive behavior at various screen widths to ensure usability

9. Validate story header renders as 'US-99: Move Lock and Codebase Initialized controls next to Provision ADO' (not 'US-99: US-99')

10. Reference supporting visual files (Screenshot 2026-02-19 021100.jpg and image.png) for layout validation


### Dependencies


- Access to dashboard/index.html file

- Supporting visual references in .ado/stories/US-99/documents/ for layout validation



---

## Risk Assessment

### Identified Risks

- CSS conflicts with existing styles could affect layout on different screen sizes

- Moving DOM elements might inadvertently break existing JavaScript event bindings if not handled carefully

- Responsive behavior might not work correctly across all device types without thorough testing


---

## Assumptions Made

- The single-file SPA structure allows direct HTML/CSS modifications without build process

- Existing JavaScript event handlers are bound by ID and will continue working after DOM restructuring

- The .top-nav section has sufficient space and appropriate styling to accommodate the additional controls

- Current CSS classes and styling will work appropriately for the new control grouping

- Supporting visual files provide accurate representation of desired final layout


---

## Testing Strategy
Manual testing approach: 1) Visual verification - compare final layout against provided screenshots to ensure correct positioning and spacing, 2) Functional testing - verify all three buttons (Provision ADO, Lock, Codebase Initialized) remain clickable and trigger correct handlers, 3) Responsive testing - test layout at multiple breakpoints including exactly 1024px, above 1024px (desktop), and below 1024px (mobile/tablet) to ensure proper wrapping behavior, 4) Cross-browser testing - verify layout works in major browsers, 5) Story header validation - confirm story title renders correctly without duplication, 6) Click target testing - ensure no overlapping click areas that could cause user interaction issues.

---

*Generated by Planning Agent*  
*Timestamp: 2026-02-19T09:34:28.9650488Z*
