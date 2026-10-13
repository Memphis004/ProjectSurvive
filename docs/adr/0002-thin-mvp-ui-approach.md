# ADR 0002 — Thin, project-owned MVP for UI

- **Status:** Accepted
- **Date:** 2026-10-13
- **Stage:** 00
- **Supersedes:** nothing

## Context

The GDD's tech-stack table says:

> MVP แบบthin — View ไม่มี logic ยังระบุ package UI.MVP Lite ไม่ได้ จึงเริ่มด้วยของโปรเจกต์เอง แล้วค่อยเปลี่ยน adapter
> ("Thin MVP — Views hold no logic; the `UI.MVP Lite` package cannot be
> identified yet, so start with the project's own and swap in an adapter later.")

Stage 00 requirement 8 repeats this: use a thin, project-owned MVP until the
`UI.MVP Lite` package is identified.

So there is no third-party MVP library to depend on, and no confirmed repository
for `UI.MVP Lite`. We must not invent one (AGENTS rule 2: do not guess package
APIs/versions), and we must not build an abstraction for a feature that has no
current reason to exist (AGENTS rule 12).

## Decision

1. **Do not add a UI/MVP framework dependency now.** There is no confirmed
   package to target.
2. **Adopt the thinnest possible MVP discipline by convention**, enforced by
   review and, where practical, by assembly boundaries — not by a framework:
   - A **View** holds no game logic. It does not compute prices, combos, or
     damage. It reads/writes only its own serialized widgets.
   - A **Presenter** calls **Application use cases only** — never Domain mutation
     directly, never another Presenter's View.
   - The **Application → Presentation** boundary is one-directional: state flows
     out as `R3` streams carrying **UI-level state only** (HP, currency, bag,
     language). Entity positions are never pushed through R3.
3. **If `UI.MVP Lite` is supplied later**, it is adopted behind the existing
   boundaries. Because Views are logic-free and Presenters only call use cases,
   swapping the mechanism should touch the UI layer only — the Domain and
   Application layers must not need changes.
4. **Record the dependency as `Blocked`** in
   [`docs/architecture/dependencies.md`](../architecture/dependencies.md#23-blocked--unverified)
   until a repository URL is provided.

## Alternatives considered

- **Pick a third-party MVVM/MVP framework now** (e.g. a signal-bus or MVVM
  package). Rejected: it would be a guessed dependency, and AGENTS forbids
  guessing package APIs. Also risks coupling Domain to a UI library.
- **Plain uGUI with logic in MonoBehaviours.** Rejected: violates the GDD's thin
  MVP rule and makes the Stage 03 UI-tech spike harder to evaluate.

## Consequences

- **Positive:** no unverifiable dependency; nothing to remove if `UI.MVP Lite`
  never materializes; the Stage 03 Thai-text spike can compare uGUI+TMP vs.
  UI Toolkit without a framework in the way.
- **Negative:** "thin MVP" is a convention, not a compiler-enforced boundary.
  It needs an architecture test or review checklist to stay honest. Candidate
  check for Stage 14: assert Presentation assemblies do not reference Domain
  mutation types.
- **Follow-up:** when `UI.MVP Lite` is identified, write a short addendum ADR
  rather than editing this one.
