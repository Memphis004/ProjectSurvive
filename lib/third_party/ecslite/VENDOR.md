# EcsLite (vendored) — provenance

LeoECS Lite by Leopotam — the ECS used by the **battle simulation**
(`ProjectSurvive.Core.Simulation`).

This folder is **third-party source, copied verbatim**. Do not edit it by hand;
change the pinned revision instead (procedure below).

| Field | Value |
| --- | --- |
| Upstream | https://github.com/Leopotam/ecslite |
| Pinned revision | `d46f3b209ea163810823aeea7d2610d38ad6cdf8` |
| Pinned version string | `2026.4.25` (from `package.json`; upstream tag `2026.4.25-0`) |
| Commit date | 2026-04-25 |
| Vendored on | 2026-10-13 (Stage 01) |
| License | **MIT-Red** — see [`LICENSE.md`](LICENSE.md), copied verbatim |
| Distribution | vendored source in-tree (no UPM/registry dependency) |

## What is vendored

| Path | Content |
| --- | --- |
| `LICENSE.md` | upstream license, verbatim |
| `README.md` | upstream documentation, verbatim |
| `package.json` | upstream package manifest (records version `2026.4.25`) |
| `Leopotam.EcsLite.asmdef` | upstream assembly definition, verbatim |
| `src/*.cs` | `components`, `entities`, `filters`, `systems`, `worlds` — verbatim |

Upstream `.meta` files are **not** vendored: this repository has no Unity asset
database yet (first Unity open is Stage 03), so Unity will generate them. Nothing
references EcsLite by GUID — `Simulation` references it by assembly name.

## Notes that matter for this project

1. **`noEngineReferences` is `false` upstream and is left untouched.** The
   `#if ENABLE_IL2CPP` blocks use `Unity.IL2CPP.CompilerServices` attributes, so
   the assembly *does* need engine references when Unity compiles a Web
   (IL2CPP) build. Outside Unity — i.e. in the `dotnet` harness — those blocks
   are excluded and the library is pure BCL. This is the one deliberate
   exception to the project's `noEngineReferences: true` default and it applies
   to *vendored* code, not to `lib/Runtime`.
2. **Not thread-safe, by design** (upstream README). Fine: WebGL2 is
   single-threaded and the battle tick is single-threaded regardless.
3. **No reflection in the core**, so AOT/IL2CPP code stripping is not a risk.
4. The upstream project is **feature-frozen** ("поддержка прекращена") and
   declared stable with no known bugs. Treat it as a fixed dependency; do not
   expect fixes upstream.
5. **MIT-Red is not plain MIT.** It adds restrictions (no military use; no use
   by parties hostile to Russians) on top of the MIT grant. Recorded as a
   licensing note in `docs/architecture/dependencies.md`.

## How to update the pinned revision

```bash
# 1. fetch the wanted revision and inspect the diff before copying anything
git clone https://github.com/Leopotam/ecslite /tmp/ecslite-vendor
git -C /tmp/ecslite-vendor checkout <new-revision>
git -C /tmp/ecslite-vendor log -1 --format='%H %ad %s'

# 2. copy verbatim (LICENSE.md always, plus README.md / package.json / asmdef / src)
cp /tmp/ecslite-vendor/LICENSE.md /tmp/ecslite-vendor/README.md \
   /tmp/ecslite-vendor/package.json /tmp/ecslite-vendor/Leopotam.EcsLite.asmdef \
   lib/third_party/ecslite/
cp /tmp/ecslite-vendor/src/*.cs lib/third_party/ecslite/src/

# 3. update the table above, tools/versions.lock.json, and
#    docs/architecture/dependencies.md, then run tools/test.ps1
```

An upgrade is a **dependency change**: it needs the project owner's approval
(`AGENTS.md` rule 2) and the new revision must be recorded in
`tools/versions.lock.json`.
