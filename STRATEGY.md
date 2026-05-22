# Rexl — Project Strategy

This file is the source of truth for the autonomous commander agent that drives
development cycles on `microsoft/rexl`. It captures the project's mission,
current state, prioritized goals, explicit non-goals, success signals, and the
constraints under which autonomous work must operate. Future agent cycles
should read this file before deciding what to do next.

## Mission

Rexl (Research Expression Language) is a statically-typed, pure functional
expression language with a larger statement-oriented dialect, RexlScript,
designed to be embedded in applications. It shares heritage with
[Microsoft Power Fx](https://github.com/microsoft/Power-Fx) and combines a
classical expression core (lexer, parser, binder, CIL code generator) with
data-flow graphs, ONNX model invocation, and SAT/MIP solver integration so
that researchers and embedders can express data transformations, machine
learning inference, and combinatorial optimization in a single, composable
language. The long-term vision is a stable, well-documented embeddable
language with high-quality NuGet packages, cross-platform parity, runnable
samples that exercise every major feature, and a Jupyter authoring experience
that makes Rexl approachable to new users.

## Current state

What exists in the repository today (with source paths):

- Core language libraries under `src/Core/`:
  - `Rexl.Base` — type system (`DType`), name handling, utilities.
  - `Rexl.Bind` — lexer, parser, binder, parse tree, and bound (semantic) tree
    for both Rexl and RexlScript.
  - `Rexl.Code` — MSIL/CIL code generation and `DType` ↔ `System.Type`
    mapping (the type manager).
  - `Rexl.Flow` — flow-graph functionality (data nodes, formula nodes,
    formula-defined edges).
  - `Rexl.Onnx` — [ONNX Runtime](https://github.com/microsoft/onnxruntime)
    integration that exposes sample models from the
    [ONNX Model Zoo](https://github.com/onnx/models) as Rexl functions.
  - `Rexl.Solve` — SAT solver and linear MIP solver integrations
    (HiGHS is the default MIP solver per PR #38; GLPK and Gurobi are also
    integrated).
  - `Rexl.Harness` — host harness for executing RexlScript, including its
    parallel-task concept.
- Sample applications under `src/Apps/`:
  - `RexlBench` — GUI editor/runner for RexlScript.
  - `RexlRun` — command-line RexlScript runner.
  - `DocBench` — GUI flow-graph editor and evaluator.
  - `Kernel` — Jupyter kernel (`RexlKernel`, `RexlKernel.Base`) with
    `RegisterKernel.cmd` (Windows) and `reg.sh` (Unix) registration helpers.
- Benchmarks: `src/Benchmark/Rexl.Benchmark`.
- Tests under `src/Test/`: roughly 27 `*Tests.cs` files and ~1,075 `.txt`
  baseline scripts across `Rexl.Base.Test`, `Rexl.Bind.Test`,
  `Rexl.Code.Test` (with `Scripts/Block`, `Scripts/CodeGen`, `Scripts/Json`,
  `Scripts/Module`, `Scripts/TypeManager`, `Scripts/Util` corpora),
  `Rexl.Flow.Test`, `Rexl.Onnx.Test`, and `Rexl.Solve.Test`. A `Baseline/`
  tree and shared `*.TestBase` projects support golden-file testing.
- Two solutions in `src/`: `Rexl.sln` (Windows, includes WinForms apps)
  and `RexlCrossPlat.sln` (Linux/macOS, library/CLI subset; added in PR #44).
- Documentation under `docs/`:
  - `docs/README.md` index.
  - `docs/Grammars.md` — lexical and syntactic grammars.
  - `docs/UserGuide/RexlUserGuide.md` with five sections under
    `docs/UserGuide/Sections/` (`01-AboutRexl` through `05-Functions`),
    covering **core Rexl only** — RexlScript is explicitly out of scope of
    the current user guide.
  - `docs/activity-log.md` — commander activity log.
- Samples under `samples/`:
  - `samples/notebooks/` — 10 Jupyter notebooks (NFL, Comma, ImageAsTensor,
    ImageClassification, ProteinFolding, EssentialMedicines,
    HexagonalPuzzleSat, SudokuSat, SudokuMip, SudokuMipEq).
  - `samples/data/` — `.rexl` data scripts, `.rbin` binary tables, and image
    assets used by the notebooks.
  - `samples/README.md` describing each notebook.
- NuGet packaging metadata in `nuget/` (`NUGET.md`, `nuget-package.props`).
  Published packages on nuget.org (per `README.md`): `Microsoft.Rexl.Base`,
  `Microsoft.Rexl.Bind`, `Microsoft.Rexl.Code`, `Microsoft.Rexl.Flow`,
  `Microsoft.Rexl.Harness`, `Microsoft.RexlKernel.Base`.
  `Microsoft.Rexl.Onnx` and `Microsoft.Rexl.Solve` are explicitly called out
  in the README as "will be in the future" — i.e., known gaps.
- CI under `pipelines/`: `build.yml` is the entry point and fans out to
  `build_win.yml`, `build_linux.yml`, and `build_mac.yml` (x64 and arm64),
  building both `Debug` and `Release` on every push to `main` and on PRs
  (PR build automation enabled in PR #19). `docs/`, `samples/`, and `*.md`
  changes are excluded from PR builds via the `pr.paths.exclude` list.
- Per-language editor guidance under `.github/instructions/`
  (`csharp.instructions.md`, `markdown.instructions.md`,
  `markdown-gfm.instructions.md`, etc.) and a fleet of agent definitions
  under `.github/agents/`.
- Top-level `README.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, `SUPPORT.md`,
  `LICENSE`, `NOTICE`, `.editorconfig`, and `src/stylecop.ruleset`.

Recent commit cadence (last ~30 commits, from `git log --oneline`) shows the
project is in an active maintenance-and-expansion phase: pipeline hardening
(#19, #39, #44, #45), packaging polish (#40, #41), incremental language work
(`Reverse` #34, `Mxximize` move #43, HiGHS default #38, `First`/`TakeOne`
unification #24, ResizePixels fix #37, `ConfigureAwait(false)` cleanup #36),
documentation buildout (#20–#25, #32, #35), Jupyter UX (#22, #28, #33), and
many sample notebooks (#16, #17, #26, #27, #29, #30).

## Strategic goals

The commander should make incremental progress against these prioritized
goals across cycles. Each goal is grounded in something that already exists
in the repository.

1. **Tighten cross-platform parity for `RexlCrossPlat.sln`.**
   _Rationale:_ `RexlCrossPlat.sln` was added recently in PR #44 and PR #45
   was a follow-up diagnosing a pipeline test failure. Keeping
   `dotnet build` and `dotnet test` green on Linux and macOS (x64 + arm64)
   for both `Debug` and `Release` is foundational. Work items can include
   fixing any flaky/skipped tests on non-Windows, ensuring `Rexl.Onnx.Test`
   and `Rexl.Solve.Test` pass everywhere the underlying native binaries
   ship, and keeping `pipelines/build_linux.yml` and `pipelines/build_mac.yml`
   healthy.

2. **Expand the user guide to cover RexlScript and fill core gaps.**
   _Rationale:_ `docs/UserGuide/RexlUserGuide.md` states explicitly that it
   covers "core Rexl, not RexlScript." RexlScript is supported by the
   Jupyter kernel and the sample apps, but has no narrative documentation.
   Smaller deltas — completing missing function reference entries in
   `Sections/05-Functions.md`, expanding `04-Operators.md`, adding worked
   examples — are also good incremental targets. PRs #20–#25, #32, and #35
   establish the tone and structure to match.

3. **Grow the sample-notebook and sample-script catalog around existing
   features.** _Rationale:_ The notebook set under `samples/notebooks/`
   already demonstrates tabular data, image tensors, ONNX classification,
   SAT and MIP solvers, and Rexl `module` functionality. New notebooks
   should exercise features that lack a runnable sample (e.g., flow-graph
   editing with `DocBench`, additional ONNX models, more harness/task
   scenarios in RexlScript) and reuse the existing `samples/data/` corpus
   where possible. Add a `samples/scripts/` analog for `RexlBench` /
   `RexlRun` examples if and only if the pattern is requested by an
   existing script under `src/Test/.../Scripts/`.

4. **Close the published-package gap: ship `Microsoft.Rexl.Onnx` and
   `Microsoft.Rexl.Solve`.** _Rationale:_ The `README.md` package table
   explicitly says these "will be in the future." `nuget/nuget-package.props`
   and the existing six packages already model the shape. Incremental
   tasks: confirm packaging metadata for `src/Core/Rexl.Onnx` and
   `src/Core/Rexl.Solve`, validate native-dependency handling (ONNX
   Runtime, HiGHS, GLPK), and produce a pre-release `.nupkg` from CI.

5. **Improve test coverage and reduce baseline-script churn.**
   _Rationale:_ ~1,075 `.txt` baselines under `src/Test/.../Scripts/` are a
   strong asset but make regressions easy to mask. Incremental work:
   targeted unit tests in `Rexl.Base.Test`, `Rexl.Bind.Test`, and
   `Rexl.Code.Test` for any function added or modified; baseline
   regeneration discipline; small bug-fix PRs in the spirit of #37, #34,
   and #24 driven by tests.

6. **Keep `README.md`, `docs/`, and `samples/README.md` in lockstep with
   the code.** _Rationale:_ Every new function, package, or sample should
   update its corresponding entry. PRs like #31 (sample README sync) and
   #41 (NuGet badges) show this is already part of the project's
   workflow.

## Non-goals

The commander must **not** pursue any of the following autonomously. These
require explicit human direction:

- Major language redesigns or breaking syntax changes to Rexl or RexlScript.
  Anything that changes grammar in `docs/Grammars.md` or alters the
  semantics of an existing operator/function is human-design territory.
- New top-level product directions not derivable from the existing code
  (e.g., a new front-end target, a new IL backend, a brand-new solver
  family). Stay within `src/Core/`, `src/Apps/`, `src/Test/`,
  `src/Benchmark/`, `docs/`, `samples/`, `pipelines/`, and `nuget/`.
- Edits to `LICENSE`, `NOTICE`, `SECURITY.md`, `CODE_OF_CONDUCT.md`,
  `SUPPORT.md`, or any trademark / brand text in `README.md`.
- Force-pushes, history rewrites, deletions, or any non-fast-forward
  operation on `main` or on any release tag (e.g., `v1.0.3-20240506.4`).
- Merging PRs that touch security-sensitive code (auth, deserialization,
  cryptography, native interop bindings under `Rexl.Onnx` or `Rexl.Solve`)
  without explicit human review.
- Disabling or weakening tests, baselines, or analyzers (including
  `src/stylecop.ruleset` and `.editorconfig`) to make a build pass.
- Removing or rewriting `STRATEGY.md` itself without a human edit first
  (see Out-of-cycle triggers).

## Success metrics

These are the measurable signals the commander should watch over time:

- **CI green rate on `main`:** all jobs in `pipelines/build.yml`
  (`build_win`, `build_linux`, `build_mac` × `Debug`/`Release` × `x64`/
  `arm64` where applicable) pass on every push to `main`.
- **Test count growth:** number of `*Tests.cs` files under `src/Test/` and
  number of `.txt` baselines trend upward, never down, in the absence of a
  documented consolidation.
- **Documentation surface area:** count of `.md` files under `docs/` and
  the number of subsections in `docs/UserGuide/Sections/` trend upward
  until RexlScript is covered; no `TBD` or `TODO` markers remain in
  published docs.
- **Sample coverage:** count of notebooks in `samples/notebooks/` and
  count of supporting files in `samples/data/`; each new core feature
  picks up a notebook or sample script within a few cycles.
- **Published-package completeness:** number of packages on nuget.org
  matching the `README.md` table reaches parity (i.e., `Microsoft.Rexl.Onnx`
  and `Microsoft.Rexl.Solve` shipped) and stays there.
- **Open-issue and stale-PR trend:** total open issues and the age of the
  oldest open PR do not grow over a rolling window.

## Constraints and conventions

- This is a Microsoft open-source project. Follow the CLA flow described
  in `README.md` (the CLA bot decorates each PR). Honor the
  `CODE_OF_CONDUCT.md`.
- All C# work follows `.github/instructions/csharp.instructions.md`,
  `.editorconfig`, and `src/stylecop.ruleset`. Use file-scoped namespaces,
  PascalCase public members, camelCase locals, `I`-prefixed interfaces,
  and `nameof` over string literals. Prefer `ConfigureAwait(false)` in
  library code (precedent: PR #36).
- All Markdown work follows `.github/instructions/markdown.instructions.md`
  and `.github/instructions/markdown-gfm.instructions.md` — CommonMark
  0.31.2 plus GFM extensions (tables, task lists, strikethrough).
- Preserve the existing project layout: core libraries in `src/Core/`,
  apps in `src/Apps/`, tests in `src/Test/`, benchmarks in
  `src/Benchmark/`, pipelines in `pipelines/`, packaging in `nuget/`,
  user-facing samples in `samples/`, and user-facing docs in `docs/`.
- Both solutions must stay buildable: `src/Rexl.sln` (Windows; includes
  the WinForms apps `RexlBench` and `DocBench`) and
  `src/RexlCrossPlat.sln` (Linux/macOS subset). Add new projects to the
  appropriate solution(s).
- `.gitattributes` is used for Git LFS — if a new binary file type lands
  in `samples/data/`, add an entry (the `samples/README.md` calls this
  out).
- **All work must go through PRs targeting `main`. No direct commits to
  `main`.** Use feature branches in the spirit of existing remote
  branches (e.g., `wenhanw/pr-build`, `shonk/SamplesUpdate`).
- The commander runs as the PR author and **cannot self-approve** on
  GitHub. To signal LGTM on its own PRs, use `gh pr review --comment`
  (never `--approve`) and wait for human approval before merging
  anything substantive. Trivial doc-only PRs may still be merged by the
  commander only when policy explicitly permits and CI is green.
- **Never merge a PR whose CI status is failing or pending.** Wait for
  all `pipelines/build.yml` jobs to be green.

## Out-of-cycle triggers

The commander should break out of its current plan and respond to any of
these signals before continuing routine work:

- A new human-filed GitHub issue appears in `gh issue list --state open`.
- A human (not the commander, not the CLA bot, not the
  `copilot-pull-request-reviewer`) leaves a review comment on any open PR.
  Note: a `COMMENTED` review from `copilot-pull-request-reviewer` means
  inline comments are present and must be addressed — it is not "no
  review."
- Any job in `pipelines/build.yml` fails on a `main` build.
- A push to `main` happens that was not authored by the commander.
- This `STRATEGY.md` file is edited (by a human or otherwise) — re-read
  it and reconcile any in-flight plan against the new goals before the
  next action.
- A release tag (matching the existing pattern, e.g.,
  `v1.0.3-YYYYMMDD.N`) is created or moved.
