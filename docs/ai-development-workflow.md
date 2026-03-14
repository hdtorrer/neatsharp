# AI-Assisted Development Workflow

NeatSharp is developed and maintained entirely using AI coding agents. Every feature -- from specification through implementation, testing, code review, and merge -- is authored by [Claude Code](https://docs.anthropic.com/en/docs/claude-code) guided by [Speckit](https://github.com/hdtorrer/neatsharp/.claude/commands/), a structured specification-to-implementation workflow embedded as slash commands.

This document describes the tools, configuration, and step-by-step process used to build each feature.

## Tools and Setup

### Claude Code

[Claude Code](https://docs.anthropic.com/en/docs/claude-code) is the primary development interface. It operates as an interactive CLI agent that reads and writes code, runs builds and tests, manages git operations, and executes shell commands -- all within the developer's terminal.

Key configuration files:

| File | Purpose |
|------|---------|
| `CLAUDE.md` | Auto-generated project guidelines (tech stack, commands, code style). Loaded into every conversation as context. |
| `.claude/settings.local.json` | Permission allowlist for CLI operations (build, test, format, git, etc.) |
| `.claude/commands/` | Speckit slash command definitions (markdown-based prompts) |

### Speckit

Speckit is a specification-driven development framework implemented as a set of Claude Code slash commands. It enforces a structured pipeline that transforms a natural language feature description into tested, reviewed, merged code.

Core configuration:

| File | Purpose |
|------|---------|
| `.specify/memory/constitution.md` | Project charter: 7 core principles, DI practices, coding conventions, governance rules |
| `.specify/templates/` | Templates for specs, plans, tasks, and checklists |
| `.specify/scripts/powershell/` | Automation scripts for feature scaffolding and context updates |

### Constitution

The constitution (`.specify/memory/constitution.md`) defines the project's non-negotiable principles. Every implementation plan is gated against these before and after the design phase:

1. **Correctness** -- Implement canonical NEAT per literature
2. **Reproducibility** -- Identical results with same seed/config/machine
3. **Performance** -- GPU acceleration as first-class capability
4. **Developer Experience** -- <20 LOC for basic usage, sensible defaults
5. **Minimal Dependencies** -- Only essential packages, cross-platform
6. **SOLID Design** -- All principles enforced
7. **Test-Driven Development** -- Red-Green-Refactor mandatory

Additional gates cover DI practices (constructor injection, composition root) and observability requirements.

## Feature Development Pipeline

Each feature follows this 10-step pipeline, executed entirely through Claude Code slash commands:

```
/speckit.specify
     |
/speckit.clarify  (optional, highly recommended)
     |
/speckit.plan
     |
/speckit.tasks
     |
/speckit.analyze  (optional, highly recommended -- run twice)
     |                \__ decision point: proceed or restart if gaps are too large
/speckit.implement.byphases
     |
/speckit.checklist.comp
     |
/review-interactive  (completeness review against checklist)
     |
/review-interactive  (code quality review)
     |
commit, push, PR, merge
```

### Step 1: Specification (`/speckit.specify`)

**Input:** A natural language feature description from the developer.

**What happens:**
1. Auto-generates a short feature name (2-4 words) and sequential branch number (e.g., `009-parallel-cpu-eval`)
2. Checks for duplicate branches/specs across local and remote
3. Runs `create-new-feature.ps1` to scaffold the feature directory under `specs/`
4. Generates `spec.md` from the spec template

**Output:** A feature branch and `specs/{NNN}-{name}/spec.md` containing:
- Prioritized user stories (P1, P2, P3) with acceptance scenarios (Given/When/Then)
- Functional requirements (FR-001, FR-002, ...)
- Key entities, success criteria, assumptions, and scope boundaries

### Step 2: Clarification (`/speckit.clarify`) -- Optional but Highly Recommended

**Input:** A completed spec with potentially underspecified areas.

**What happens:**
1. Analyzes the spec for ambiguities or gaps
2. Asks up to 5 targeted clarification questions
3. Encodes answers back into the spec

**Output:** Refined `spec.md` with clarifications section populated.

This step catches ambiguities early that would otherwise surface during implementation, saving significant rework.

### Step 3: Implementation Planning (`/speckit.plan`)

**Input:** Completed `spec.md`.

**What happens:**
1. Loads the constitution and evaluates the feature against all 7 principles + DI practices
2. Executes a multi-phase planning workflow:
   - **Phase 0 (Research):** Resolves unknowns, investigates existing codebase patterns, documents design decisions
   - **Phase 1 (Design):** Generates data model, API contracts, and quickstart examples
3. Runs `update-agent-context.ps1` to update `CLAUDE.md` with new technologies
4. Re-checks constitution compliance post-design

**Output:**
- `plan.md` -- Technical approach, tech stack, constitution check, project structure
- `research.md` -- Design decisions with rationale and alternatives considered
- `data-model.md` -- Entity definitions, relationships, validation rules
- `contracts/` -- API surface: interface definitions and method signatures
- `quickstart.md` -- Usage examples for the new feature

### Step 4: Task Generation (`/speckit.tasks`)

**Input:** Completed `plan.md` and `spec.md`.

**What happens:**
1. Reads all design artifacts (plan, spec, data model, contracts, research)
2. Generates a dependency-ordered task breakdown organized by phase:
   - **Phase 1 (Setup):** Project scaffolding
   - **Phase 2 (Foundational):** Blocking prerequisites
   - **Phase 3+ (Stories):** One phase per user story, in priority order
   - **Final (Polish):** Cross-cutting concerns

**Output:** `tasks.md` with checklist-formatted tasks:
```
- [ ] T006 [P] [US1] Create ParallelSyncFunctionAdapterTests.cs in tests/NeatSharp.Tests/Evaluation/
```
- `[TaskID]` -- Execution order (T001, T002, ...)
- `[P]` -- Parallelizable (different files, no blocking dependencies)
- `[Story]` -- Story phase label (US1, US2, ...)

### Step 5: Cross-Artifact Analysis (`/speckit.analyze`) -- Optional but Highly Recommended

**Input:** All design artifacts produced so far (spec, plan, research, data model, contracts, tasks).

**What happens:**
1. Performs cross-artifact consistency and quality analysis
2. Presents issues one at a time in an interactive format with 3 solution options
3. Collects developer choices and optionally applies remediation

**This step is run twice.** The first pass catches inconsistencies between artifacts (e.g., a requirement in the spec that has no corresponding task, or a contract that doesn't match the data model). The second pass validates that fixes from the first pass didn't introduce new issues.

**This is the critical decision point.** If the analysis reveals fundamental gaps or misalignments between the spec, plan, and tasks, it is often better to restart from `/speckit.specify` rather than proceed with a flawed foundation. The cost of restarting here is far lower than discovering problems during implementation.

**Output:** Remediated artifacts with cross-cutting consistency verified.

### Step 6: Implementation (`/speckit.implement.byphases`)

**Input:** Completed and validated `tasks.md` with all prerequisite artifacts.

**What happens:**
1. Validates that checklists are in place
2. Delegates each phase to a separate subagent to manage context window size
3. Each phase follows TDD Red-Green-Refactor:
   - Write tests first, ensure they fail
   - Implement the minimum code to pass
   - Refactor if needed
4. Tasks marked `[P]` run in parallel via concurrent subagents
5. Checkpoint validation between phases

The by-phases variant is preferred over `/speckit.implement` because it clears context between phases, preventing context window exhaustion on large features.

**Output:** Implementation code, tests, and incremental commits.

### Step 7: Completeness Checklist (`/speckit.checklist.comp`)

**Input:** The implemented feature (code and tests in the working tree).

**What happens:**
1. Validates all spec requirements have been correctly implemented
2. Generates an implementation completeness checklist at `checklists/implementation-completeness.md`
3. Cross-references functional requirements, user stories, and success criteria against the actual code

**Output:** `checklists/implementation-completeness.md` -- a detailed checklist showing which requirements are met, partially met, or missing. This checklist feeds directly into the next step.

### Step 8: Completeness Review (`/review-interactive`)

**Input:** The generated completeness checklist from step 7.

**What happens:**
1. Reviews the implementation against the completeness checklist
2. Identifies any requirements that are missing or incorrectly implemented
3. Presents gaps one at a time with solution options
4. Implements selected fixes after developer confirmation

This is a **spec-compliance review** -- it focuses on whether the implementation fulfills all specified requirements, not on code quality.

**Output:** Fixes for any completeness gaps, committed.

### Step 9: Code Quality Review (`/review-interactive`)

**Input:** The PR or changed files.

**What happens:**
1. Analyzes all changed files for issues across categories: security, code quality, performance, maintainability, bug risks
2. Presents issues one at a time with 3 solution options
3. Collects developer choices interactively
4. Implements all selected fixes after confirmation

This is a **code quality review** -- it focuses on bugs, performance, security, and maintainability, independent of spec compliance.

**Output:** Review fixes applied and committed.

### Step 10: Commit, Push, PR, and Merge

The developer commits remaining changes, pushes to the remote, and creates a PR (or asks Claude Code to do it). The CI pipeline validates:
- Code formatting (`dotnet format --verify-no-changes`)
- Builds across the matrix (Windows + Linux, net8.0 + net9.0)
- All tests pass (excluding GPU-only tests)
- NuGet packages pack successfully
- CI-category benchmarks run (non-blocking)

After CI passes, the repository admin performs a final human review of the PR before merging to `main`. While the code is authored by AI agents, all merges require explicit human approval.

## Feature Directory Structure

Each feature produces a consistent set of artifacts under `specs/`:

```
specs/009-parallel-cpu-eval/
  spec.md                              # Feature specification
  plan.md                              # Implementation plan
  research.md                          # Design decisions and research
  data-model.md                        # Entity definitions
  quickstart.md                        # Usage examples
  tasks.md                             # Task breakdown
  contracts/
    api-changes.md                     # API surface documentation
  checklists/
    requirements.md                    # Requirements verification
    implementation-completeness.md     # Implementation status
```

## Additional Slash Commands

These commands are available for ad-hoc use outside the main pipeline:

| Command | Purpose |
|---------|---------|
| `/speckit.constitution` | Create or update the project constitution |
| `/speckit.checklist` | Generate a custom checklist for a specific concern |
| `/speckit.taskstoissues` | Convert tasks to GitHub issues |

## How CLAUDE.md Stays Current

`CLAUDE.md` is auto-generated from feature plans and kept current by the `update-agent-context.ps1` script, which runs during the planning phase. It provides every Claude Code conversation with:

- Active technologies and dependencies per feature
- Project structure overview
- Build, test, format, and pack commands
- Code style rules
- Recent changes log

Manual additions are preserved between `<!-- MANUAL ADDITIONS START -->` and `<!-- MANUAL ADDITIONS END -->` markers.

## CI Integration

The GitHub Actions CI pipeline (`.github/workflows/ci.yml`) runs on every PR:

| Job | What it checks |
|-----|----------------|
| Format Check | `dotnet format --verify-no-changes --severity warn` |
| Build & Test | Matrix: (ubuntu + windows) x (net8.0 + net9.0), all tests except GPU |
| Pack | `dotnet pack --configuration Release` |
| Benchmark | CI-category benchmarks (non-blocking) |
| Docs Validation | README section checks + link validation via lychee |
