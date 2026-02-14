# Architecture Decision Records for {{ WORK_ITEM_ID }}

---

{{ for decision in DECISIONS }}
## ADR-{{ for.index + 1 }}: {{ decision.TITLE }}

**Date:** {{ decision.DATE }}  
**Decided By:** {{ decision.AGENT }}

### Decision
{{ decision.DECISION }}

### Rationale
{{ decision.RATIONALE }}

---
{{ end }}
