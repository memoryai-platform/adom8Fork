# Phase 14: Dashboard

**Goal:** HTML/CSS/JS dashboard with TWO intentional bugs

## Files to Create

1. `dashboard/index.html` — Single-page dashboard
2. `dashboard/staticwebapp.config.json` — Static Web App routing config

## Dashboard Structure

- Header: "🤖 AI Agent Monitor"
- 4 stat cards: Stories Processed, Avg Time, Success Rate, Time Saved
- 5 agent cards with emoji, status badge, progress bar, details
- Scrollable activity feed

## Styling

- Purple/blue gradient background (#667eea → #764ba2)
- White cards with box-shadow
- Responsive CSS grid
- Pulse animation on active agents

## INTENTIONAL BUGS

### Bug 1: getProgress() always returns 0
```javascript
function getProgress(data) { return 0; }
```
Progress bars are stuck at 0% for all agents.

### Bug 2: Completed agents show blue instead of green
```css
.agent-card.completed {
    border-left-color: #667eea; /* Should be #4caf50 */
    opacity: 0.8;              /* Should be 1.0 */
}
```

## JavaScript

- Poll GetCurrentStatus API every 2 seconds
- Configurable API URL at top of script
- Graceful error handling
- Dynamic DOM updates
