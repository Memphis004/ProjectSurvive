# A size-profile build leaves its player settings behind

**What happens.** `tools/build-web.ps1 -SizeProfile <name>` (and the three
`ProjectSurvive/Stage 03b/…` menu items) call `WebSizeProfiles.ApplyAndPersist`, which writes
player settings into `UnityProject/ProjectSettings/ProjectSettings.asset` and **persists** them.
A later build that applies no settings at all — including
`tools/build-web.ps1 -Configuration Release`, which only picks `BuildOptions` — inherits whatever
the last profile left. Build with the `optimize-size` / `minimal` residue in place and the player
halts before it draws a frame:

```
[console.log] Exception at:
Error
    at jsStackTrace (...)
    ...
[console.error] Uncaught exception from main loop:
[console.error] undefined
[console.error] Halting program.
```

No `[ProjectSurvive]` line ever appears and no page global is published, because the player dies
in the boot scene's first managed code.

**Why it looks like a code bug and is not.** With `webGLExceptionSupport: None` (which is what
those profiles set) a managed exception cannot be materialized, so both its message and its stack
are lost: the only symptom is `Uncaught undefined`. The build is *self-consistent* — it exits 0,
writes `index.html` and a `.wasm`, reports `0 errors, 1 warnings`, and the only thing wrong is a
setting nobody printed. Measured evidence from Stage 08:
`artifacts/stage08/browser-boot-crash-size-profile-residue.log` (the crash) beside
`artifacts/stage08/build-report-size-profile-residue.json` (the `sizeSettings` that produced it:
`managedStrippingLevel: High`, `exceptionSupport: None`, `il2cppCodeGeneration: OptimizeSize`).

**How to tell which configuration a build actually used.** Never infer it from the file name.
Read `UnityProject/Builds/Web/build-report.json`:

```jsonc
"sizeProfile": "none",
"sizeSettings": {
  "managedStrippingLevel": "Low",              // the authored value
  "il2cppCodeGeneration": "OptimizeSpeed",
  "exceptionSupport": "ExplicitlyThrownExceptionsOnly",
  "wasmArithmeticExceptions": "Throw"
}
```

The authored configuration is managed stripping **Low** with exceptions
**ExplicitlyThrownExceptionsOnly** (`ProjectSetup` sets the stripping level, and
`WebSizeProfilesTests.Baseline_is_the_configuration_the_project_already_had` asserts the pair is
the project's own). Anything else in that block is a profile's residue.

**How to undo it.** `git checkout -- UnityProject/ProjectSettings/ProjectSettings.asset`, then
check `git diff` after every size-profile run. Rebuilding with the authored settings restored the
player to a working state in Stage 08 with no other change.

**The general shape.** A measurement tool that mutates the thing it measures has to say so
loudly. [ADR 0011](../../docs/adr/0011-webgl-payload-budget-and-size-measurement.md) warns about
this from the other side (a GUI Build click rewrites player settings); this is the same trap with
the CLI's own measurement path. A `make-baseline`-style reset between profiles, or having
`build-web.ps1` print `sizeSettings` and refuse a mismatched pair, is the fix the next stage that
touches the build should make.
