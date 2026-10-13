#!/usr/bin/env node
// ProjectSurvive - static file server for the WebGL build (Stage 03).
//
// Why not `npx serve` or `python -m http.server`: a WebGL player only works when
// the server sends the right content types. The compressed formats in particular
// are decided by the build settings, and a wrong `Content-Type` on the `.wasm`
// shows up as a fetch/instantiate error inside the player, not as a server error,
// which is a slow thing to debug. This server is 60 lines and sets them
// explicitly, so the browser test exercises the build rather than the server.
//
//   node tools/serve-web.mjs [--root <dir>] [--port <port>]
//
// Default root: UnityProject/Builds/Web (what tools/build-web.ps1 produces).
// Prints "serving <dir> at http://127.0.0.1:<port>" once it is listening, so a
// test harness can wait for that line instead of sleeping.

import { createServer } from "node:http";
import { createReadStream, existsSync, statSync } from "node:fs";
import { extname, join, normalize, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { dirname } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..");

function parseArguments(argv) {
  const options = { root: join(repoRoot, "UnityProject", "Builds", "Web"), port: 8080 };

  for (let i = 0; i < argv.length; i += 1) {
    if (argv[i] === "--root" && argv[i + 1]) {
      options.root = resolve(argv[i + 1]);
      i += 1;
    } else if (argv[i] === "--port" && argv[i + 1]) {
      options.port = Number.parseInt(argv[i + 1], 10);
      i += 1;
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

const { root, port } = parseArguments(process.argv.slice(2));

if (!existsSync(root)) {
  console.error(`[serve-web] build directory not found: ${root}`);
  console.error("[serve-web] run tools/build-web.ps1 first, or pass --root <dir>");
  process.exit(2);
}

const server = createServer((request, response) => {
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

  const contentType = CONTENT_TYPES.get(extname(target).toLowerCase()) ?? "application/octet-stream";
  response.writeHead(200, {
    "Content-Type": contentType,
    "Cache-Control": "no-store",
    // The player is single-threaded by design (AGENTS rule 6); no COOP/COEP headers
    // are sent because no SharedArrayBuffer is used.
  });

  createReadStream(target).pipe(response);
});

server.listen(port, "127.0.0.1", () => {
  console.log(`serving ${root} at http://127.0.0.1:${port}`);
});
