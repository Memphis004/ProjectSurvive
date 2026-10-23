# Serving the WebGL player (release hosting contract)

A WebGL player is not one file and not one request: `index.html` starts a loader that
fetches the framework, the `.wasm` and the `.data`. Every one of those has a content
type, an encoding and a cache policy, and all three can fail *inside the player*,
where the symptom reads like a build bug. This page is the contract the release
candidate was tested under; `tests/web/scripts/verify-hosting.mjs` is the command
that checks it, and it runs as a step of the Full suite (`web: hosting (compression,
headers, stale cache)`).

Measured evidence: `artifacts/perf/hosting.json` (server checks) and
`artifacts/perf/performance.md` (the browser numbers).

## 1. Compression

**The player is built with Unity's `WebGLCompressionFormat` set to Disabled, on
purpose.** That setting does not decide what the payload costs (ADR 0011 §2): it
only decides whether Unity also writes `.br`/`.gz` siblings next to the raw files.
Compression is a property of the delivered bytes, so it is produced once, from the
raw artifact:

```powershell
node tools/compress-web.mjs --dir UnityProject/Builds/Web --label release-candidate
```

This writes `<file>.br` (Brotli q11, lgwin 22) and `<file>.gz` (gzip -9) for every
compressible file and `compression-report.json` with per-file and initial-payload
totals in ADR 0011's three bases. One artifact, one encoding decision, measured in
one place.

A static host then negotiates:

| Request | Response |
| --- | --- |
| `Accept-Encoding: br` + a `.br` sibling exists | `.br` bytes, `Content-Encoding: br` |
| `Accept-Encoding: gzip` + a `.gz` sibling exists | `.gz` bytes, `Content-Encoding: gzip` |
| anything else, or no sibling | the raw file, no `Content-Encoding` |

Every response that can differ by encoding carries `Vary: Accept-Encoding`. Without
it a shared cache hands Brotli bytes to a client that asked for gzip, and the failure
is a `.wasm` that will not instantiate.

`tools/serve-web.mjs` implements exactly this (it is the local stand-in for the
production host), and `tests/web/scripts/verify-hosting.mjs` checks the three cases
against the bytes on disk.

## 2. Headers

| Path | Content-Type | Cache-Control | Why |
| --- | --- | --- | --- |
| `/index.html` | `text/html; charset=utf-8` | `no-cache` | the entry point; a stale copy makes a deploy look like it did nothing |
| `/Build/*.wasm` | `application/wasm` | `no-cache` | the browser refuses a `.wasm` served as anything else |
| `/Build/*.data` | `application/octet-stream` | `no-cache` | asset pack |
| `/Build/*.js` | `text/javascript; charset=utf-8` | `no-cache` | loader/framework |
| `/TemplateData/*` | by extension | `public, max-age=3600` | decoration around the canvas (logos, progress bar, CSS) |
| `/build-info.json`, `/build-report.json`, `/compression-report.json` | `application/json` | `no-store` | diagnostics for **this** build; a cached one is a lie |

All responses: `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`.
No `Cross-Origin-Opener-Policy` / `Cross-Origin-Embedder-Policy`: the game is
single-threaded and uses no `SharedArrayBuffer` (AGENTS rule 6), so cross-origin
isolation would only break font and audio fetches for no gain.

`deploy/_headers` (Netlify / Cloudflare Pages syntax) is the same table in the form
those hosts read. A host with its own config (IIS `web.config`, nginx) must
reproduce the cache column; the encoding column is automatic there.

## 3. Why the player files are `no-cache`, not `immutable`

`ProjectSettings` keeps `webGLNameFilesAsHashes: 0`, so the loader requests
`Build/Web.wasm` and `Build/Web.data` — names that do **not** change between builds.
`Cache-Control: immutable` is only correct for content-hashed names; with stable
names it would pin every returning player to the first build they ever fetched, and
the symptom would be "the new build works for me and not for the tester".

`no-cache` still allows the browser to keep the bytes: it must revalidate them, and
the server answers `304 Not Modified` when the `ETag` matches. That is a few hundred
bytes per file per load instead of a 20 MB download, and it cannot serve a stale
player. The stale-cache check in `verify-hosting.mjs` changes a served file under a
live browser, reloads, and asserts the new bytes arrive.

If a later stage turns on content-hashed names, the policy should move to
`public, max-age=31536000, immutable` for `/Build/*` and this section should be
rewritten with the measurement that shows it is safe.

## 4. What the release checklist runs

```powershell
# once per artifact, after the build
node tools/compress-web.mjs --dir UnityProject/Builds/Web --label release-candidate
node tests/web/scripts/verify-hosting.mjs --dir UnityProject/Builds/Web --port 8091
```

Exit codes: `0` all checks passed, `1` a check failed, `2` the tool could not run
(no player, server did not start). The same two commands run inside
`tools/test.ps1 -Suite Full` as the `web: hosting` step, so a release candidate
cannot pass its gate while being served unencoded or with a stale-cache policy.
