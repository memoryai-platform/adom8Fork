# Live Demo Guide

## Setup (Before Demo)

### 1. Prep Work Items

Create 1-2 user stories in Azure DevOps. Example:

**Story 1 (the demo story):**
- Title: "Progres bars on dashbord dont work and completed ones arent green"
- Description: "The dashboard has broken progress bars that always show 0%. Also, when an agent completes, it shows blue instead of green."
- State: New (don't trigger yet)

### 2. Verify Dashboard Bugs Are Visible

Open the dashboard URL. Confirm:
- Progress bars are stuck at 0% for all agents
- Completed agents show blue border (not green)

### 3. Test Run

Run through the full flow once privately to verify all agents complete successfully.

---

## The Demo (5 Minutes)

### Act 1: Show the Broken Dashboard (30 seconds)

> "This is our AI agent monitoring dashboard. It tracks our autonomous development agents in real-time. But look at these progress bars — they're all stuck at zero. And these completed agents? They should be green, but they're showing blue. Classic dashboard bugs."

Point out the broken elements visually.

### Act 2: Write a Vague User Story (1 minute)

Open Azure DevOps. Create a new User Story. Type live, casually, with typos:

> Title: "Progres bars on dashbord dont work also completed ones arent green"

> Description: "the progres bars always show 0% even when agents are running. and when they finish they show blue instead of green. pretty sure its a css thing or maybe the javascript"

Set the state to **"Story Planning"**. This fires the service hook.

> "Notice I'm writing this like a non-technical person would — vague, typos, no code references. Let's see what our AI agents do with this."

### Act 3: Watch Agents Work (2-3 minutes)

Switch to the dashboard. Narrate each agent:

1. **Planning Agent** (15-20 seconds)
   > "The planning agent is analyzing our vague bug report. It's identifying the actual technical issues — the `getProgress()` function always returns 0, and the CSS class for completed agents uses blue instead of green."

2. **Coding Agent** (20-30 seconds)
   > "Now the coding agent is generating the fix based on the planner's analysis. It's writing the corrected `getProgress()` function and updating the CSS."

3. **Testing Agent** (15-20 seconds)
   > "Tests are being generated for the fix."

4. **Review Agent** (15-20 seconds)
   > "The code review agent is scoring the changes. If it scores above 90, it proceeds to documentation automatically."

5. **Documentation Agent** (10-15 seconds)
   > "Documentation is being generated for the PR."

### Act 4: The Fix (30 seconds)

> "The agents have created a complete pull request — planning analysis, code fix, tests, code review, and documentation. All from a vague, typo-filled bug report."

Show the `.ado/stories/US-{id}/` folder with all generated files.

> "From vague bug report to production-ready fix in under 3 minutes. Zero human intervention."

### The Closer

> "This is what autonomous development looks like. The AI didn't just fix a bug — it understood intent from a casual description, planned the approach, wrote the code, tested it, reviewed it, and documented it. And every step is tracked and auditable."

---

## Backup Plans

### If service hook doesn't fire
Manually queue a message to `agent-tasks` with the work item ID.

### If an agent fails
Check Application Insights logs. The poison queue catches failed messages after 5 retries.

### If live demo is too risky
Have a screen recording of a successful run ready. Present with: "Let me show you a run we captured earlier."

---

## Talking Points

- **Queue-based architecture** — No timeouts. Each agent can take as long as needed.
- **Multi-provider AI** — Switch between Claude, OpenAI, or Azure OpenAI with a config change.
- **Every decision is auditable** — Check `.ado/stories/US-{id}/` for complete history.
- **Score-based routing** — Code review score determines next step (docs vs. QA vs. revision).
- **Intentional bugs** — The dashboard bugs are the demo. The AI fixes the very tool it's being monitored on.
