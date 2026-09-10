# Three-stage review workflow for AI-assisted RFCs

This workflow is optional and applies only to AI-assisted RFCs.
Handwritten RFCs follow the existing [RFC process](README.md#the-process).

## Review stages

Read the full draft, suggestions, approval decisions, related RFCs, and relevant code.
Verify claims against these sources, not the drafting agent's account.

1. **AI screening:** Apply the [authoring guidance](.claude/skills/authoring-rfcs/SKILL.md), including its size comparisons. If revision is needed, stop before design review. For unnecessary length, request a shorter rewrite.
2. **AI design review:** Check the technical contract, language interactions, safety, compatibility, and distinguishing examples. Identify omissions even in detailed drafts.
3. **Human review:** After both AI stages pass, hand the draft, findings, and unresolved questions to maintainers for review.

In each reply, separate a brief, polite introduction linking the guidance and size comparisons from categorized RFC-specific bullet fragments.
Use categories such as **Not needed**, **Missing**, **Repeated**, **Conflicting**, and **Incorrect**.
Link the proposed guidance revision until it is merged, then use the shared main-branch link.

## Record results

Record the reviewed HEAD, guidance revision, and supporting evidence.
Use a review independent of drafting to assign a pass.
The labels below record AI review results, not maintainer approval.

| Stage | Revise | Pass |
| --- | --- | --- |
| AI screening | `AI-Review-1-screening-redo` | `AI-Review-1-screening-OK` |
| AI design review | `AI-Review-2-design-redo` | `AI-Review-2-design-OK` |

Reject stale or conflicting results.
Clear both AI stages after new commits. Re-screening clears the AI design review.
Publish comments or change labels only when authorized.
