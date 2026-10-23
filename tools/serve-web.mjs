#!/usr/bin/env node
// ProjectSurvive - static file server for the WebGL build (Stage 03, hosting policy Stage 15).
//
// Why not `npx serve` or `python -m http.server`: a WebGL player only works when
// the server sends the right content types. The compressed formats in particular
// are decided by the build settings, and a wrong `Content-Type` on the `.wasm`
// shows up as a fetch/instantiate error inside the player, not as a server error,
// which is a slow thing to debug. This server is ~200 lines and sets them
// explicitly, so the browser test exercises the build rather than the server.
//
//   node tools/serve-web.mjs [--root <dir>] [--port <port>] [--no-compress]
//
// Default root: UnityProject/Builds/Web (what tools/build-web.ps1 produces).
// Prints "serving <dir> at http://127.0.0.1:<port>" once it is listening, so a
// test harness can wait for that line instead of sleeping.
//
// Stage 15 hosting contract (docs/build/web-hosting.md):
//   * the player is built with Unity's compression DISABLED, so the files on disk
//     are the raw bytes; `tools/compress-web.mjs` writes `.br` / `.gz` siblings and
//     this server negotiates them from `Accept-Encoding`, exactly as a static host
//     does. One copy of the payload, one place where the encoding decision exists.
//   * every response that can differ by encoding carries `Vary: Accept-Encoding`;
//     a cache that ignores it serves Brotli bytes to a client that cannot decode
//     them - the failure looks like a corrupt `.wasm`, not a cache bug.
//   * player file names are stable across builds (`webGLNameFilesAsHashes: 0`), so
//     they are served `no-cache` + `ETag` and revalidated. `immutable` caching is
//     only correct for content-hashed names, and the loader's names are not.
//   * `build-info.json` / `build-report.json` are build diagnostics, not payload:
//     `no-store`, so a Reader cannot mistake a stale report for this build's.

import { createServer } from "node:http";
import { createReadStream, existsSync, statSync } from "node:fs";
import { extname, join, normalize, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { dirname } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..");

function parseArguments(argv) {
  const options = {
    root: join(repoRoot, "UnityProject", "Builds", "Web"),
    port: 8080,
    compress: true,
  };

  for (let i = 0; i < argv.length; i += 1) {
    if (argv[i] === "--root" && argv[i + 1]) {
      options.root = resolve(argv[i + 1]);
      i += 1;
    } else if (argv[i] === "--port" && argv[i + 1]) {
      options.port = Number.parseInt(argv[i + 1], 10);
      i += 1;
    } else if (argv[i] === "--no-compress") {
      options.compress = false;
    }
  }

  return options;
}

// Unity serves .js, .wasm, .data and the Unity framework files; the browser refuses
// a .wasm that is not application/wasm.
const CONTENT_TYPES = new Map([
  [".html", "text/html; charset=utf-8"],
  [".js", "text/javascript; charset=utf-8"],
  [".mjs", "text/javascript; charset=utf-8"],
  [".wasm", "application/wasm"],
  [".data", "application/octet-stream"],
  [".mem", "application/octet-stream"],
  [".symbols", "application/octet-stream"],
  [".json", "application/json; charset=utf-8"],
  [".css", "text/css; charset=utf-8"],
  [".png", "image/png"],
  [".jpg", "image/jpeg"],
  [".svg", "image/svg+xml"],
  [".ico", "image/x-icon"],
  [".txt", "text/plain; charset=utf-8"],
  [".br", "application/octet-stream"],
  [".gz", "application/octet-stream"],
]);

// Diagnostics a build writes beside the player: true for this build only.
const BUILD_DIAGNOSTICS = new Set(["build-info.json", "build-report.json", "compression-report.json"]);

// Files the loader fetches before the game runs (ADR 0011's measured unit). They
// get the strictest cache policy; everything else is decoration around the canvas.
const PLAYER_PAYLOAD = new Set([".html", ".js", ".wasm", ".data"]);

function etagFor(stat) {
  return `"${stat.size.toString(16)}-${Math.floor(stat.mtimeMs).toString(16)}"`;
}

function cacheControlFor(relativePath) {
  const name = relativePath.split("/").pop();
  if (BUILD_DIAGNOSTICS.has(name)) {
    return "no-store";
  }

  // The canvas is loaded from index.html; a stale one is the "my deploy did
  // nothing" bug. Revalidate it every time (cheap: 304 with no body).
  if (relativePath === "index.html" || PLAYER_PAYLOAD.has(extname(relativePath).toLowerCase())) {
    return "no-cache";
  }

  // Template decoration. One hour is short enough that a restyled player cannot be
  // hidden by a cache and long enough not to refetch on every reload.
  return "public, max-age=3600";
}

function accepts(header, encoding) {
  if (!header) {
    return false;
  }

  const accepted = String(header)
    .split(",")
    .map((part) => part.trim().toLowerCase())
    .filter(Boolean);

  for (const entry of accepted) {
    const [name, ...parameters] = entry.split(";").map((piece) => piece.trim());
    if (name !== encoding && name !== "*") {
      continue;
    }

    const q = parameters.find((parameter) => parameter.startsWith("q="));
    return q ? Number.parseFloat(q.slice(2)) > 0 : true;
  }

  return false;
}

/**
 * The file to send and the encoding it carries.
 *
 * `.br` wins over `.gz` when both exist and both are accepted, which is what a
 * production static host does (Brotli is smaller and every supported browser
 * negotiates it). Falls back to the raw file, which is what makes swapping in a
 * host that does not compress a *performance* change, not a correctness one.
 */
function selectVariant(target, acceptEncoding, compress) {
  const extension = extname(target).toLowerCase();
  const compressible = compress && ![".br", ".gz", ".png", ".jpg", ".ico"].includes(extension);

  if (compressible) {
    for (const [encoding, suffix] of [
      ["br", ".br"],
      ["gzip", ".gz"],
    ]) {
      const candidate = target + suffix;
      if (accepts(acceptEncoding, encoding) && existsSync(candidate)) {
        return { path: candidate, encoding };
      }
    }
  }

  return { path: target, encoding: null };
}

function resolveRequestPath(root, urlPath) {
  const withoutQuery = decodeURIComponent(urlPath.split("?")[0]);
  const relative = normalize(withoutQuery).replace(/^([/\\])+/, "");
  const candidate = join(root, relative === "" ? "index.html" : relative);

  // Refuse anything that escaped the root (normalize collapses "..", the prefix
  // check catches the rest).
  if (!candidate.startsWith(root + sep) && candidate !== root) {
    return null;
  }

  return candidate;
}

const { root, port, compress } = parseArguments(process.argv.slice(2));

if (!existsSync(root)) {
  console.error(`[serve-web] build directory not found: ${root}`);
  console.error("[serve-web] run tools/build-web.ps1 first, or pass --root <dir>");
  process.exit(2);
}

// A HEAD is the same headers with no body; the hosting verifier uses it to read
// the negotiated encoding without transferring the player.
function sendFile(request, response, filePath, relativePath, encoding) {
  const stat = statSync(filePath);
  const etag = etagFor(stat);
  const contentType = CONTENT_TYPES.get(extname(relativePath).toLowerCase()) ?? "application/octet-stream";

  const headers = {
    "Content-Type": contentType,
    "Cache-Control": cacheControlFor(relativePath),
    ETag: etag,
    "X-Content-Type-Options": "nosniff",
    "Referrer-Policy": "no-referrer",
    // The player is single-threaded by design (AGENTS rule 6): no COOP/COEP
    // headers, because no SharedArrayBuffer is used and cross-origin isolation
    // would only break audio/font requests for no gain.
  };

  if (encoding) {
    headers["Content-Encoding"] = encoding;
    headers.Vary = "Accept-Encoding";
  }

  const ifNoneMatch = request.headers["if-none-match"];
  if (ifNoneMatch && ifNoneMatch.split(",").some((value) => value.trim() === etag)) {
    response.writeHead(304, headers).end();
    return;
  }

  headers["Content-Length"] = stat.size;
  response.writeHead(200, headers);

  if (request.method === "HEAD") {
    response.end();
    return;
  }

  createReadStream(filePath).pipe(response);
}

const server = createServer((request, response) => {
  if (request.method !== "GET" && request.method !== "HEAD") {
    response.writeHead(405, { Allow: "GET, HEAD" }).end("method not allowed");
    return;
  }

  const filePath = resolveRequestPath(root, request.url ?? "/");

  if (filePath === null) {
    response.writeHead(403).end("forbidden");
    return;
  }

  let target = filePath;
  if (existsSync(target) && statSync(target).isDirectory()) {
    target = join(target, "index.html");
  }

  if (!existsSync(target)) {
    response.writeHead(404, { "Content-Type": "text/plain; charset=utf-8" }).end("not found: " + request.url);
    return;
  }

  const relativePath = target.startsWith(root + sep) ? target.slice(root.length + 1) : target;
  const variant = selectVariant(target, request.headers["accept-encoding"], compress);
  sendFile(request, response, variant.path, relativePath, variant.encoding);
});

server.listen(port, "127.0.0.1", () => {
  const compressed = compress ? "br > gzip from .br/.gz siblings" : "disabled";
  console.log(`serving ${root} at http://127.0.0.1:${port}`);
  console.log(`[serve-web] encoding: ${compressed}; player files: no-cache + ETag; TemplateData: max-age=3600`);
});
