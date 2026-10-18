# ADR 0003 — Performance baseline machine and browser

- **Status:** Accepted
- **Date:** 2026-10-13
- **Inherited by:** template-v1 — the decision still applies; the numbering below is the template's own (0001–0099).
- **Stage:** 00

## Context

The GDD sets a performance budget (Stage 15) with targets such as:

- Test resolution **1920×1080**, desktop.
- Browsers: **Chromium and Firefox**, with the tested version recorded.
- Frame time **p95 ≤ 20 ms** on normal floors.
- Stress case **~300 enemies + 500 projectiles**.
- Memory ~350 MB for the Unity/Wasm heap, measured separately from GPU.
- Initial download ≤ 30 MB compressed.
- Allocation near zero after warm-up.

The GDD also states the numbers are **not yet measured** and that in-Editor FPS is
**not** valid evidence — only a real browser counts. Stage 00 must therefore fix
*which machine and which browser* a baseline refers to, so later numbers are
comparable instead of anecdotal.

## Decision

1. **Reference machine (baseline):** the developer workstation measured in
   Stage 00.

   | Component | Value |
   | --- | --- |
   | CPU | Intel Core i5-14400F (10 cores / 16 threads) |
   | RAM | 31.8 GiB |
   | GPU | NVIDIA GeForce RTX 5060 Ti, driver 32.0.16.1692 |
   | OS | Windows 11 (10.0.26300) x64 |

2. **Reference viewport:** **1920×1080**, browser zoom 100%, no OS scaling
   applied to the canvas.

3. **Reference browsers:**
   - **Primary — Chromium:** Google Chrome `154.0.8037.98` (user install).
   - **Secondary — Chromium:** Microsoft Edge `145.0.3800.70` as a fallback if
     Chrome is unavailable.
   - **Required — Gecko:** Firefox — **currently NOT installed** on the reference
     machine. Until it is installed, Firefox results are reported **NOT RUN**,
     never skipped silently.
   - The **exact browser version is recorded with every measurement.** A number
     without a browser version and the machine spec is not a baseline.

4. **Measurement rules (binding on later stages):**
   - Decide performance from **real browser builds**, never Editor FPS.
   - Every reported number carries: machine spec, browser + version, build
     config (Development/Release), and the scenario (floor, entity counts).
   - Report a distribution (p50 / p95 frame time), not a single frame.
   - Numbers come from **actual measurement**. Unmeasured targets stay labeled
     "target", never "achieved".

5. **This ADR fixes the environment only.** No performance figure is measured or
   claimed in Stage 00.

## Alternatives considered

- **Defer the baseline machine until Stage 15.** Rejected: budgets compound over
  ~15 stages, and without a fixed reference machine early numbers are not
  comparable.
- **Use in-Editor Play mode FPS as the primary signal.** Rejected by the GDD, and
  it does not exercise IL2CPP/Wasm at all.

## Consequences

- **Positive:** any "before/after" comparison in a later stage refers to the same
  hardware and named browser version.
- **Negative:** results are specific to this machine; that is expected, and
  cross-machine comparison is explicitly out of scope (determinism is likewise
  only guaranteed within one runtime).
- **Follow-up:** install Firefox before Stage 14 (or mark Gecko as NOT RUN and
  say so in the report).
