# RFC review workflow

Authors use [authoring-rfcs](.claude/skills/authoring-rfcs/SKILL.md) to write and revise their RFCs.
Reviewers read the full RFC, suggestions, approval history, related designs, and relevant code.
Approval follows actual decisions, not labels or votes alone.

1. **Screening, 100,000 ft:** approved scope, proportional length, expert writing. Request a shorter rewrite before discussing design details when repetition obscures the design.
2. **Design, 30,000 ft:** only after screening passes. Check the technical contract, language interactions, safety, compatibility, and distinguishing examples.
3. **Human review:** only after both stages pass.

| Stage | Revise | Pass |
| --- | --- | --- |
| Screening | `AI-Review-1-screening-redo` | `AI-Review-1-screening-OK` |
| Design | `AI-Review-2-design-redo` | `AI-Review-2-design-OK` |

Record the reviewed HEAD and accepted guidance revisions. Passes require independent review and current approval evidence.
Reject stale or conflicting results. Clear both stages after new commits. Re-screening clears design.

Use a polite, shared introduction linking the authoring skill, followed by short, RFC-specific revision bullets.
Link the proposed skill revision until it is merged, then use the shared main-branch link.
Publish comments or change labels only when authorized.
