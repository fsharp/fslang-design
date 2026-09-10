# Reviewing AI-assisted RFCs

This optional AI review helps authors prepare AI-assisted drafts for maintainer review.
The existing [RFC process](README.md#the-process) accepts handwritten and AI-assisted RFCs without AI review.
Authors remain responsible for their specifications. AI review does not approve a design or replace maintainer judgment.

## Review the draft

Read the full draft, suggestions, approval decisions, related RFCs, and relevant code.
Verify claims against these sources, not the drafting agent's account.

1. **Screening:** Check approved scope, proportional length, and expert writing against the [authoring guidance](.claude/skills/authoring-rfcs/SKILL.md). Request a shorter rewrite when repetition obscures the design.
2. **Design:** After screening passes, check the technical contract, language interactions, safety, compatibility, and distinguishing examples. Identify omissions even in detailed drafts.

Give polite, RFC-specific revision requests rather than a generic checklist.
Link the proposed guidance revision until it is merged, then use the shared main-branch link.

## Record results

Record the reviewed HEAD, guidance revision, and supporting evidence.
Use a review independent of drafting to assign a pass.
The labels below record AI review results, not maintainer approval.

| Stage | Revise | Pass |
| --- | --- | --- |
| Screening | `AI-Review-1-screening-redo` | `AI-Review-1-screening-OK` |
| Design | `AI-Review-2-design-redo` | `AI-Review-2-design-OK` |

Reject stale or conflicting results.
Clear both stages after new commits. Re-screening clears design.
Publish comments or change labels only when authorized.
