# Gotcha — a GUI Build click rewrites player settings

Building from **File → Build Settings** (or that window's Build button) applies
platform profile defaults over the project's player settings:
`webGLNameFilesAsHashes`, the compression format, linear vs gamma lighting and the
shared IL2CPP/Bee tree. After one click the project's own configuration is no longer
what the player was built with, and a later CLI build inherits the residue.

The game-era example: a size-profile build persisted `exceptionSupport: None` +
stripping High, a later Release build inherited them, and the player halted at boot
with `Uncaught undefined` — which is what "no exception support" looks like — while
the build reported 0 errors. See
[`webgl-size-profile-settings-residue.md`](webgl-size-profile-settings-residue.md).

**Only these count as evidence:**

- `tools/build-web.ps1` (Development or Release), optionally with a `-SizeProfile`;
- the size measured by `tools/measure-web-size.mjs`, quoting the configuration the
  build recorded in `build-report.json → sizeSettings`.

A build started from a window is a build with unknown settings: read
`build-report.json`, and if there is none, do not quote the number. The decision is
[ADR 0011](../../docs/adr/0011-webgl-payload-budget-and-size-measurement.md).
