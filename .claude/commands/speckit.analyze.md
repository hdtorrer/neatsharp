---
description: Interactive cross-artifact consistency and quality analysis that presents issues one-by-one with 3 solution options, collects user feedback, then optionally applies remediation.
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty). The input may specify:
- Specific types of issues to focus on (e.g., "coverage gaps", "ambiguity", "constitution")
- Severity filter (e.g., "critical only", "high and above")

## Goal

Perform an interactive consistency and quality analysis across the three core artifacts (`spec.md`, `plan.md`, `tasks.md`) that engages the user in decision-making for each identified issue. Unlike a standard analysis that dumps all findings at once, this command presents issues sequentially, offers solution alternatives, and collects user preferences before suggesting any remediation.

## Operating Constraints

**READ-ONLY UNTIL REMEDIATION APPROVED**: Do not modify any files until all issues have been reviewed and user explicitly approves remediation.

**INTERACTIVE FLOW**: Present exactly ONE issue at a time. Wait for user input before proceeding to the next issue.

**SOLUTION OPTIONS**: For each issue, provide exactly 3 distinct solution approaches with your recommended option clearly highlighted.

**Constitution Authority**: The project constitution (`.specify/memory/constitution.md`) is **non-negotiable**. Constitution conflicts are automatically CRITICAL and require adjustment of the spec, plan, or tasks—not dilution, reinterpretation, or silent ignoring of the principle.

## Execution Steps

### 1. Initialize Analysis Context

Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` once from repo root and parse JSON for FEATURE_DIR and AVAILABLE_DOCS. Derive absolute paths:

- SPEC = FEATURE_DIR/spec.md
- PLAN = FEATURE_DIR/plan.md
- TASKS = FEATURE_DIR/tasks.md

Abort with an error message if any required file is missing (instruct the user to run missing prerequisite command).
For single quotes in args like "I'm Groot", use escape syntax: e.g 'I'\''m Groot' (or double-quote if possible: "I'm Groot").

### 2. Load Artifacts (Progressive Disclosure)

Load only the minimal necessary context from each artifact:

**From spec.md:**
- Overview/Context
- Functional Requirements
- Non-Functional Requirements
- User Stories
- Edge Cases (if present)

**From plan.md:**
- Architecture/stack choices
- Data Model references
- Phases
- Technical constraints

**From tasks.md:**
- Task IDs
- Descriptions
- Phase grouping
- Parallel markers [P]
- Referenced file paths

**From constitution:**
- Load `.specify/memory/constitution.md` for principle validation

### 3. Build Semantic Models

Create internal representations (do not include raw artifacts in output):

- **Requirements inventory**: Each functional + non-functional requirement with a stable key
- **User story/action inventory**: Discrete user actions with acceptance criteria
- **Task coverage mapping**: Map each task to one or more requirements or stories
- **Constitution rule set**: Extract principle names and MUST/SHOULD normative statements

### 4. Detection Passes (Token-Efficient Analysis)

Focus on high-signal findings. Build a prioritized queue of issues.

#### A. Duplication Detection
- Identify near-duplicate requirements
- Mark lower-quality phrasing for consolidation

#### B. Ambiguity Detection
- Flag vague adjectives (fast, scalable, secure, intuitive, robust) lacking measurable criteria
- Flag unresolved placeholders (TODO, TKTK, ???, `<placeholder>`, etc.)

#### C. Underspecification
- Requirements with verbs but missing object or measurable outcome
- User stories missing acceptance criteria alignment
- Tasks referencing files or components not defined in spec/plan

#### D. Constitution Alignment
- Any requirement or plan element conflicting with a MUST principle
- Missing mandated sections or quality gates from constitution

#### E. Coverage Gaps
- Requirements with zero associated tasks
- Tasks with no mapped requirement/story
- Non-functional requirements not reflected in tasks

#### F. Inconsistency
- Terminology drift (same concept named differently across files)
- Data entities referenced in plan but absent in spec (or vice versa)
- Task ordering contradictions
- Conflicting requirements

### 5. Build Issue Queue with Severity

Prioritize issues using this heuristic:

- **CRITICAL**: Violates constitution MUST, missing core spec artifact, or requirement with zero coverage that blocks baseline functionality
- **HIGH**: Duplicate or conflicting requirement, ambiguous security/performance attribute, untestable acceptance criterion
- **MEDIUM**: Terminology drift, missing non-functional task coverage, underspecified edge case
- **LOW**: Style/wording improvements, minor redundancy not affecting execution order

Limit to a maximum of 20 issues for focused review. If more issues exist, note overflow count for potential follow-up.

### 6. Present Initial Summary

Before starting the interactive loop, give a quick overview:

---

## Analysis Overview

**Artifacts Analyzed:**
- `spec.md` - [X requirements, Y user stories]
- `plan.md` - [X phases, Y components]
- `tasks.md` - [X tasks across Y phases]

**Issues Found:** [Total count]
- Critical: [N]
- High: [N]
- Medium: [N]
- Low: [N]

**Coverage:** [X]% of requirements have associated tasks

---

**Ready to review issues one by one?**
- `yes` - Start interactive review
- `critical` - Only review CRITICAL issues
- `high` - Review CRITICAL and HIGH issues only
- `cancel` - Skip interactive review, show summary report only

---

### 7. Interactive Issue Presentation Loop

For each issue in the queue, present it in this exact format:

---

#### Issue [N] of [Total] | Severity: [CRITICAL/HIGH/MEDIUM/LOW]

**Category:** [Duplication/Ambiguity/Underspecification/Constitution/Coverage/Inconsistency]

**Location(s):** `[file_path]:[line_number or section reference]`

**Description:**
[Clear explanation of what the issue is and why it matters]

**Current State:**
> [Quote or paraphrase the problematic content]

---

**Solution Options:**

| Option | Approach | Trade-offs |
|--------|----------|------------|
| **A (Recommended)** | [Primary solution - specific edit to make] | [Pros and cons] |
| B | [Alternative approach - different way to resolve] | [Pros and cons] |
| C | Skip - Acknowledge and leave as-is | No change; document as accepted risk |

**Why Option A is recommended:** [1-2 sentence explanation based on context]

---

**Your choice?** Reply with:
- `A`, `B`, or `C` to select an option
- `yes` or `recommended` to accept Option A
- `skip` to skip this issue without any fix
- `stop` to end the review early

---

**Rules for the loop:**
- Present EXACTLY ONE issue at a time
- Wait for user response before showing the next issue
- Record each choice in working memory (do not apply yet)
- If user says "stop", "done", or "end", exit the loop early
- After the last issue (or early termination), proceed to Step 8

### 8. Present Summary and Confirm Remediation

After all issues have been reviewed, present a summary:

---

## Review Summary

**Issues Reviewed:** [N] of [Total found]

| # | Category | Severity | Location | Your Choice | Planned Action |
|---|----------|----------|----------|-------------|----------------|
| 1 | Coverage | CRITICAL | spec.md:FR-3 | A (Recommended) | Add task for requirement |
| 2 | Ambiguity | HIGH | spec.md:NFR-2 | B | Add measurable criteria |
| 3 | Duplication | MEDIUM | spec.md:L45-52 | Skip | No change |
| ... | ... | ... | ... | ... | ... |

**Remediation Required:** [N] issues selected for fixes
**Skipped:** [N] issues

**Metrics:**
- Total Requirements: [N]
- Total Tasks: [N]
- Coverage: [X]%
- Issues to Remediate: [N]

---

**Ready to generate remediation plan?**
- `yes` or `remediate` - Generate specific edit suggestions for selected fixes
- `modify` - Go back and change specific choices
- `cancel` - End without remediation (choices recorded but no edits suggested)

---

### 9. Generate Remediation Plan

Only after user confirms with "yes" or "remediate":

For each issue where user selected Option A or B (not Skip/C), provide:

---

## Remediation Plan

### Fix #1 - [Brief issue description]

**File:** `[artifact path]`
**Section:** [Section name or line reference]

**Current:**
```markdown
[Current content]
```

**Proposed:**
```markdown
[Suggested replacement]
```

---

[Repeat for each fix]

---

**Apply these changes?**
- `yes` or `apply` - Apply all remediation edits
- `partial [numbers]` - Apply only specific fixes (e.g., `partial 1,3,5`)
- `cancel` - Abort without making changes

---

### 10. Apply Selected Remediation

Only after explicit user approval:

1. Apply each edit in order
2. Note what was changed in each file
3. Preserve formatting and style of existing artifacts

After implementation:

---

## Remediation Complete

**Files Modified:**
- `spec.md` - [Brief description of changes]
- `plan.md` - [Brief description of changes]
- `tasks.md` - [Brief description of changes]

**Recommended Next Steps:**
- Review changes: `git diff`
- Re-run analysis to verify: `/speckit.analyze`
- If CRITICAL issues remain, resolve before `/speckit.implement`

---

### 11. Handle Edge Cases

**No issues found:**
> "Analysis complete. No significant issues found across spec.md, plan.md, and tasks.md. Coverage: [X]%. Ready for implementation!"

**User provides "modify" after summary:**
> "Which issue number would you like to change? (Enter number or 'list' to see summary again)"

**User cancels:**
> "Analysis complete. No remediation applied. Your choice records are not saved."

**User selects "summary only" at start:**
Output the traditional summary report format (findings table, coverage summary, metrics) without interactive flow.

**User stops early:**
Proceed to Step 8 with only the issues reviewed so far.

## Behavior Rules

- **Never auto-remediate**: Always wait for explicit user confirmation before modifying files
- **Be concise**: Keep issue descriptions clear and actionable
- **Respect user choices**: If user picks option B or C, honor that choice without second-guessing
- **Maintain context**: Remember all choices throughout the session
- **One issue at a time**: Never present multiple issues in a single message
- **Honest recommendations**: Base recommendations on actual artifact context, not generic rules
- **Skip gracefully**: "Skip" is always a valid choice; don't pressure users to fix everything
- **Prioritize constitution**: Constitution violations are always CRITICAL and Option A should always resolve the conflict

## Context

$ARGUMENTS
