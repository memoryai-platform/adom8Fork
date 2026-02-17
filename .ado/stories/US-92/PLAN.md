# Planning Analysis for US-92

## Story Overview

**ID:** US-92  
**Title:** US-92: Move health status indicators from header to left sidebar  
**State:** Story Planning  
**Created:** 2026-02-17

### Description
<p>Move the ADO, Queue, AI, Config, and Git status indicators currently in the dashboard header down into the left sidebar. The sidebar has plenty of room under the queue section. This declutters the header and makes better use of sidebar space. </p>

<h3>Technical Details </h3>
<p>The dashboard is a single-file SPA at <code>dashboard/index.html</code> (~4100 lines). All CSS, JS, and HTML are in this one file. </p>

<h4>Current Location (to remove from): </h4>
<ul>
<li>HTML: <code>div.nav-health#nav-health</code> inside <code>div.top-nav</code> (around lines 2314-2358) </li>
<li>Contains 5 <code>div.nav-health-item</code> elements with IDs: nh-ado, nh-queue, nh-ai, nh-config, nh-git </li>
<li>Each has: <code>span.nav-health-dot</code> (colored circle), <code>span.nav-health-label</code> (text), <code>div.nav-health-tooltip</code> (hover popup) </li>
<li>Plus poison message counter: <code>span#nav-health-poison</code> </li>
</ul>

<h4>Target Location (to move to): </h4>
<ul>
<li>HTML: Inside <code>aside.sidebar-left</code> (line ~2402), below the existing Totals stats section (div.sidebar-stats, lines ~2412-2443) </li>
<li>Add a new section with header &quot;System Health&quot; matching the sidebar-stats styling pattern </li>
</ul>

<h4>CSS Classes Involved: </h4>
<ul>
<li>Current header styles to repurpose: .nav-health, .nav-health-item, .nav-health-dot, .nav-health-label, .nav-health-tooltip, .nav-health-poison (lines ~335-500) </li>
<li>Dot status classes: .healthy (#4caf50), .degraded (#ff9800), .unhealthy (#f44336), .unknown (#666) </li>
<li>Sidebar styles to match: .sidebar-stats, .sidebar-header, .sidebar-stat-item, .sidebar-stat-label (lines ~504-545) </li>
<li>Layout will need to change from horizontal (flex-row in header) to vertical (flex-column in sidebar) </li>
</ul>

<h4>JavaScript (no logic changes needed): </h4>
<ul>
<li>fetchHealth() - fetches /api/health every 60s (line ~3814) </li>
<li>updateHealthPanel(data) - updates DOM via document.querySelectorAll('.nav-health-item') (line ~3825) </li>
<li>These query by CSS class, so moving the HTML elements preserves JS functionality as long as class names stay the same </li>
</ul>

<h4>Responsive Behavior: </h4>
<ul>
<li>Existing mobile breakpoint at 900px hides .sidebar-left entirely (line ~901) </li>
<li>Health indicators follow the same collapse behavior - no special mobile handling needed </li>
</ul>

<h3>Acceptance Criteria </h3>
<ul>
<li>All 5 status indicators (ADO, Queue, AI, Config, Git) removed from header </li>
<li>Status indicators added to left sidebar below the Totals/queue section </li>
<li>Status indicators retain same styling (green/red dots with labels) </li>
<li>Header is cleaner with more space </li>
<li>Hover tooltips still work </li>
<li>Health polling unchanged (60s interval, /api/health endpoint) </li>
</ul>

### Acceptance Criteria
<ol>
<li>All 5 health status indicators (ADO, Queue, AI, Config, Git) are removed from the top navigation bar (the div.nav-health#nav-health container inside div.top-nav). </li>
<li>A new &quot;System Health&quot; section is added to the left sidebar (aside.sidebar-left) below the existing &quot;Totals&quot; stats section (div.sidebar-stats). </li>
<li>Each indicator retains the same styling: 8px colored dot (.nav-health-dot) with status classes: healthy (green #4caf50), degraded (orange #ff9800), unhealthy (red #f44336), unknown (gray #666). </li>
<li>Each indicator retains its label (ADO, Queue, AI, Config, Git) and hover tooltip showing status + detail text. </li>
<li>The poison message counter (#nav-health-poison) moves with the health indicators to the sidebar. </li>
<li>The top navigation bar is visually cleaner with only the logo, project name, dark mode toggle, and emergency stop button remaining. </li>
<li>The health data source and polling interval remain unchanged: fetches from /api/health every 60 seconds via the existing fetchHealth() and updateHealthPanel() JavaScript functions. </li>
<li>Responsive: on mobile/tablet (existing breakpoint at 900px), the sidebar collapses as it does today - health indicators follow the same responsive behavior as the Queue and Totals sections. </li>
</ol>

---

## Technical Analysis

### Problem Analysis
This is a UI refactoring task to relocate health status indicators from the dashboard header to the left sidebar. The story involves moving 5 health indicators (ADO, Queue, AI, Config, Git) plus a poison message counter from div.nav-health in the top navigation to a new 'System Health' section in the left sidebar below the existing Totals section. The change improves header cleanliness and better utilizes sidebar space. All existing functionality (60s polling, tooltips, styling) must be preserved.

### Recommended Approach
This is a pure frontend change in the single-file SPA dashboard/index.html. The approach involves: 1) Remove the existing div.nav-health container from the header (lines 2314-2358), 2) Create a new 'System Health' section in the sidebar following the existing sidebar-stats pattern, 3) Adapt CSS from horizontal header layout (.nav-health with flex-row) to vertical sidebar layout (matching .sidebar-stats with flex-column), 4) Preserve all existing CSS classes for JavaScript compatibility, 5) Maintain responsive behavior where sidebar collapses at 900px breakpoint. No JavaScript changes needed since the existing fetchHealth() and updateHealthPanel() functions query by CSS class names.

### Affected Files

- `dashboard/index.html`


### Complexity Estimate
**Story Points:** 5

### Architecture Considerations
Single-file SPA modification with HTML structure changes, CSS layout adaptation from horizontal to vertical, and preservation of existing JavaScript functionality through CSS class compatibility.

---

## Implementation Plan

### Sub-Tasks

1. Remove div.nav-health#nav-health container from header (lines 2314-2358)

2. Create new 'System Health' section in left sidebar below Totals section

3. Adapt CSS classes from horizontal header layout to vertical sidebar layout

4. Update .nav-health-item styling to match sidebar-stat-item pattern

5. Ensure poison message counter moves with health indicators

6. Verify responsive behavior follows existing 900px breakpoint

7. Test that JavaScript polling and DOM updates continue working

8. Validate tooltip positioning works in new sidebar location


### Dependencies


- Existing fetchHealth() JavaScript function must continue working

- Existing updateHealthPanel() DOM queries by CSS class must remain compatible

- Sidebar responsive behavior at 900px breakpoint must be preserved

- Health API endpoint /api/health must remain unchanged



---

## Risk Assessment

### Identified Risks

- CSS class name changes could break JavaScript functionality

- Tooltip positioning might need adjustment in sidebar context

- Responsive behavior could be affected if sidebar styling is modified incorrectly

- Visual alignment with existing sidebar sections might require fine-tuning


---

## Assumptions Made

- The dashboard/index.html file structure matches the described line numbers

- Existing JavaScript functions fetchHealth() and updateHealthPanel() work by CSS class selectors

- The sidebar has sufficient vertical space for 5 health indicators plus poison counter

- Current tooltip implementation will work in the sidebar context without modification

- The 60-second polling interval and /api/health endpoint remain unchanged


---

## Testing Strategy
Manual testing approach: 1) Verify health indicators are removed from header and appear in sidebar, 2) Confirm all 5 indicators (ADO, Queue, AI, Config, Git) plus poison counter are present, 3) Test that colored dots maintain proper status colors (green/orange/red/gray), 4) Verify hover tooltips display correctly, 5) Confirm JavaScript polling continues every 60 seconds, 6) Test responsive behavior by resizing browser to <900px width, 7) Validate visual alignment with existing sidebar sections, 8) Check that header appears cleaner with only logo, project name, dark mode toggle, and emergency stop button remaining.

---

*Generated by Planning Agent*  
*Timestamp: 2026-02-17T18:31:49.2298982Z*
