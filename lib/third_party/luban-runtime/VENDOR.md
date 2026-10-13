# Vendor — Luban C# runtime (`Luban.Runtime`)

This folder carries the **C# runtime that Luban-generated code compiles
against**. It is vendored source, exactly like `lib/third_party/ecslite`: it is
small, stable, has no external dependencies, and pinning the bytes is what makes
`tools/verify-generated.ps1` reproducible.

## What is here

| File | Purpose |
| --- | --- |
| `src/ByteBuf.cs` | The binary reader (`Luban.ByteBuf`) that reads the generated `.bytes` tables. |
| `src/BeanBase.cs` | `Luban.BeanBase` — the base class of every generated bean. |
| `src/ITypeId.cs` | `Luban.ITypeId`. |
| `src/StringUtil.cs` | `Luban.StringUtil` — used by the generated `ToString()` overloads. |
| `LICENSE.md` | The upstream MIT license, copied verbatim. |
| `Luban.Runtime.asmdef` | Unity assembly definition (`noEngineReferences: true`). |

Only the **`cs-bin`** target dependencies are vendored. The JSON targets
(`cs-simple-json`, …) additionally need `Luban.SimpleJSON`, which is deliberately
not vendored because this project loads the binary format only.

## Provenance

| Field | Value |
| --- | --- |
| Project | [focus-creative-games/luban](https://github.com/focus-creative-games/luban) |
| Release | `v5.1.0` (tag), CLI reports `Luban 5.1.0+df34215d2e22035073d1af83d44e3ac0744dc3b3` |
| Source revision | `df34215d2e22035073d1af83d44e3ac0744dc3b3` |
| Upstream path | `src/Luban.Tests/RuntimeSupport/` |
| License | MIT (see `LICENSE.md`) |

`src/Luban.Tests/RuntimeSupport/` is the runtime Luban's own test suite compiles
generated code against; the release archive (`Luban.7z`) ships the CLI/DLLs but
**not** this C# runtime, so the runtime is copied from the pinned source tag.

## Integrity

Verify a copy with `sha256sum` (or `certutil -hashfile <f> SHA256`):

| File | SHA-256 |
| --- | --- |
| `src/BeanBase.cs` | `2B3FB576F2D2CB3B6C74C2FFD8C75C4F035AB42D857547B1A9C0D892D5C45201` |
| `src/ByteBuf.cs` | `C784561036714B73A23834969A3C3426CE32F0AA15690E47AD3AC4C56FE0F017` |
| `src/ITypeId.cs` | `EFFD56BE2EC82D7650B23172AD396341CF7248F7CA34DFEDA362F88A6C1BB477` |
| `src/StringUtil.cs` | `513ED1B536CAC41CD1BBB0CFF42466F4F289255422B2E6149BE67097951A791D` |

## How to update

1. Get owner approval: AGENTS rule 2 forbids a dependency upgrade without it.
2. Re-pin the Luban release in `tools/versions.lock.json` (`vendoredTools`) and
   re-run `tools/setup-tools.ps1`.
3. Clone the new tag, copy `src/Luban.Tests/RuntimeSupport/{BeanBase,ITypeId,ByteBuf,StringUtil}.cs`
   verbatim, refresh the hash table above, and regenerate.

Adding a **new** runtime file is a breaking change for the generated code
contract: re-run `tools/gen.ps1` and `tools/verify-generated.ps1` afterwards.

## Notes

- The sources are byte-for-byte upstream; only `LICENSE.md` and this document
  are project additions. `lib/Runtime/Generated/**` is machine output and must
  never be hand-edited (AGENTS rule 4).
- `ByteBuf.cs` guards its unaligned-memory paths behind
  `#if CPU_SUPPORT_MEMORY_NOT_ALIGN`, which is **not** defined — the Web build
  and the `dotnet` harness therefore compile the same aligned code path.
