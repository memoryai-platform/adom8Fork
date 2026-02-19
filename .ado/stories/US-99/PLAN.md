# Planning Analysis for US-99

## Story Overview

**ID:** US-99  
**Title:** Move Lock and Codebase Initialized controls next to Provision ADO  
**State:** Story Planning  
**Created:** 2026-02-19

### Description
<div>As an operator, I want the Lock button and Codebase Initialized control next to Provision ADO on the Dashboard so controls are visible, grouped, and not blocking actions. </div><div><br> </div><div><b>Repository/UI implementation context (verified):</b> </div><div>- Frontend is a single-file SPA at <code>dashboard/index.html</code>. </div><div>- Provision ADO control is currently rendered in the top nav as button id <code>provision-btn</code>. </div><div>- Lock control is currently rendered in header tools as button id <code>function-key-btn</code>. </div><div>- Codebase status control is currently rendered in header tools as span id <code>codebase-badge</code> with scan button id <code>btn-scan-codebase</code>. </div><div>- Current behavior hooks: <code>handleProvisionAdo()</code>, <code>openFunctionKeyPrompt()</code>, <code>openScanModal()</code>/<code>confirmCodebaseScan()</code>. </div><div><br> </div><div><b>Current layout baseline:</b> </div><div>- <code>provision-btn</code> is in the <code>.top-nav</code> section. </div><div>- <code>function-key-btn</code> and <code>codebase-badge</code> are in <code>.header-tools</code> inside the main header container. </div><div>- This story moves Lock and Codebase controls to sit next to Provision ADO in the same top-nav controls group, preserving existing behavior and IDs. </div><div><br> </div><div>Supporting visual references for implementation: </div><div>- Screenshot 2026-02-19 021100.jpg (AttachedFile) </div><div>- image.png (pasted image in description) </div>

### Acceptance Criteria
<div><b>Acceptance Criteria</b> </div><ul><li>Frontend file to modify is <code>dashboard/index.html</code> (single-file SPA). </li><li>Move controls so top-nav order is exactly: Provision ADO (<code>provision-btn</code>) -&gt; Lock (<code>function-key-btn</code>) -&gt; Codebase Initialized (<code>codebase-badge</code>). </li><li>Preserve existing IDs and click handlers: <code>provision-btn</code>/<code>handleProvisionAdo()</code>, <code>function-key-btn</code>/<code>openFunctionKeyPrompt()</code>, <code>codebase-badge</code> + <code>btn-scan-codebase</code>/<code>openScanModal()</code>. </li><li>Desktop (&gt;=1024px): all three controls visible on one row, no overlap, all click targets fully usable. </li><li>Tablet/mobile ( Lock -&gt; Codebase Initialized, with no click-target overlap. </li><li>Story header render rule: show <code>US-99: Move Lock and Codebase Initialized controls next to Provision ADO</code> when title exists; never <code>US-99: US-99</code>. </li><li>Implementation and validation must use supporting files: Screenshot 2026-02-19 021100.jpg and image.png. </li> </ul>

---

## Technical Analysis

### Problem Analysis
This is a UI layout reorganization task for the dashboard SPA. Currently, the Provision ADO button is in the top navigation, while the Lock button and Codebase Initialized control are in the header tools section. The goal is to move all three controls to the same top navigation area for better visual grouping and accessibility. The story provides specific element IDs, current locations, and desired final layout order.

### Recommended Approach
This is a pure frontend HTML/CSS reorganization task within the single-file SPA at dashboard/index.html. The implementation involves: 1) Locating the existing controls by their IDs (provision-btn, function-key-btn, codebase-badge), 2) Moving the Lock and Codebase controls from .header-tools to .top-nav section, 3) Arranging them in the specified order, 4) Ensuring responsive behavior for desktop and mobile viewports, 5) Preserving all existing JavaScript event handlers and functionality. No backend changes or external API calls are required.

### Affected Files

- `dashboard/index.html`


### Complexity Estimate
**Story Points:** 5

### Architecture Considerations
Frontend-only change within the existing single-file SPA architecture. The dashboard uses vanilla JavaScript with inline CSS and HTML structure. Controls will be relocated within the DOM while preserving their existing IDs, classes, and event handlers to maintain backward compatibility.

---

## Implementation Plan

### Sub-Tasks

1. Locate current positions of provision-btn, function-key-btn, and codebase-badge elements in dashboard/index.html

2. Move function-key-btn from .header-tools to .top-nav section, positioned after provision-btn

3. Move codebase-badge and btn-scan-codebase from .header-tools to .top-nav section, positioned after function-key-btn

4. Update CSS styling to ensure proper spacing and alignment in the new layout

5. Implement responsive behavior for desktop (>=1024px) showing all controls in one row

6. Implement responsive behavior for tablet/mobile (<1024px) with proper wrapping and no overlap

7. Verify all existing click handlers remain functional after DOM restructuring

8. Test the story header render rule to display 'US-99: Move Lock and Codebase Initialized controls next to Provision ADO'


### Dependencies


- Access to supporting visual references: Screenshot 2026-02-19 021100.jpg and image.png

- Current dashboard/index.html file structure and existing CSS classes



---

## Risk Assessment

### Identified Risks

- Potential CSS layout conflicts when moving controls between different container sections

- Risk of breaking existing JavaScript event handlers if DOM structure changes unexpectedly

- Responsive design challenges ensuring no click-target overlap on smaller screens

- Visual alignment issues if the controls have different styling contexts in their new location


---

## Assumptions Made

- The dashboard/index.html file contains the current layout structure as described

- Existing CSS classes (.top-nav, .header-tools) are properly defined and styled

- JavaScript event handlers are bound by element ID and will continue working after DOM reorganization

- Supporting visual references provide clear guidance for the desired final layout

- No backend functionality changes are required - this is purely a frontend layout adjustment


---

## Testing Strategy
Manual testing approach: 1) Visual verification that controls appear in correct order (Provision ADO → Lock → Codebase Initialized) in the top navigation, 2) Functional testing of all three controls to ensure click handlers work correctly, 3) Responsive testing on desktop (>=1024px) to verify single-row layout with no overlap, 4) Responsive testing on tablet/mobile (<1024px) to verify proper wrapping behavior, 5) Cross-browser compatibility testing, 6) Verification that story header displays correctly as 'US-99: Move Lock and Codebase Initialized controls next to Provision ADO', 7) Comparison against supporting visual references to ensure implementation matches expected design

---

*Generated by Planning Agent*  
*Timestamp: 2026-02-19T09:28:31.4916508Z*
