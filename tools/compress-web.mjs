#!/usr/bin/env node
// ProjectSurvive - write the pre-compressed siblings a static host serves.
//
// The player is built with Unity's `WebGLCompressionFormat` set to Disabled on
// purpose (ADR 0011 §2: the setting decides whether Unity *also* writes `.br`/`.gz`
// files, not what the payload costs). Compression is a property of the delivered
// bytes, so it is produced here, once, from the raw artifact:
//
//   node tools/compress-web.mjs [--dir UnityProject/Builds/Web] [--label release-candidate]
//
// For every compressible file it writes `<file>.br` (Brotli q11) and `<file>.gz`
// (gzip -9) next to the source, then `compression-report.json` with per-file sizes
// and the totals in ADR 0011's three bases. `tools/serve-web.mjs` and a production
// host both negotiate these siblings; a host that ignores them still works, just
// with more bytes on the wire.
//
// Exit codes: 0 = siblings written, 1 = the directory is not a player, 2 = the
// script could not run.

import { createHash } from "node:crypto";
import { existsSync, readdirSync, readFileSync, statSync, writeFileSync } from "node:fs";
import { extname, join, relative, resolve } from "node:path";
import { brotliCompressSync, constants as zlibConstants, gzipSync } from "node:zlib";
import { fileURLToPath } from "node:url";
import { dirname } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..");

function parseArguments(argv) {
  const options = {
    dir: join(repoRoot, "UnityProject", "Builds", "Web"),
    label: "web",
    quiet: false,
  };

  for (let i = 0; i < argv.length; i += 1) {
    if (argv[i] === "--dir" && argv[i + 1]) {
      options.dir = resolve(argv[i + 1]);
      i += 1;
    } else if (argv[i] === "--label" && argv[i + 1]) {
      options.label = argv[i + 1];
      i += 1;
    } else if (argv[i] === "--quiet") {
      options.quiet = true;
    }
  }

  return options;
}

// The five files ADR 0011 defines as the initial payload. A budget is quoted on
// these and nothing else; TemplateData is decoration around the canvas.
const PAYLOAD_ROLES = new Map([
  ["index.html", "html"],
  [".loader.js", "loader"],
  [".framework.js", "framework"],
  [".wasm", "wasm"],
  [".data", "data"],
]);

function payloadRole(relativePath) {
  if (relativePath === "index.html") {
    return "html";
  }
  if (!relativePath.startsWith("Build/")) {
    return null;
  }
  for (const [suffix, role] of PAYLOAD_ROLES) {
    if (suffix.startsWith(".") && relativePath.endsWith(suffix)) {
      return role;
    }
  }
  return null;
}

const COMPRESSIBLE = new Set([".html", ".js", ".mjs", ".css", ".json", ".wasm", ".data", ".mem", ".txt", ".svg"]);
const SKIP = new Set([".br", ".gz", ".png", ".jpg", ".ico", ".unityweb"]);

function walk(directory) {
  const found = [];
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const full = join(directory, entry.name);
    if (entry.isDirectory()) {
      found.push(...walk(full));
    } else if (entry.isFile()) {
      found.push(full);
    }
  }
  return found;
}

const { dir, label, quiet } = parseArguments(process.argv.slice(2));

if (!existsSync(join(dir, "index.html"))) {
  console.error(`[compress-web] no index.html under ${dir} - is that a WebGL player?`);
  process.exit(1);
}

const sha256 = (buffer) => createHash("sha256").update(buffer).digest("hex");

const files = [];
let rawTotal = 0;
let brTotal = 0;
let gzTotal = 0;
const payload = { uncompressed: 0, gzip: 0, brotli: 0, files: [] };

for (const path of walk(dir)) {
  const extension = extname(path).toLowerCase();
  const relativePath = relative(dir, path).replace(/\\/g, "/");

  if (SKIP.has(extension) || relativePath === "compression-report.json") {
    continue;
  }

  if (!COMPRESSIBLE.has(extension)) {
    files.push({ path: relativePath, skipped: true, size: statSync(path).size });
    continue;
  }

  const raw = readFileSync(path);
  const brotli = brotliCompressSync(raw, {
    params: {
      [zlibConstants.BROTLI_PARAM_QUALITY]: 11,
      [zlibConstants.BROTLI_PARAM_LGWIN]: 22,
      [zlibConstants.BROTLI_PARAM_SIZE_HINT]: raw.length,
    },
  });
  const gzip = gzipSync(raw, { level: 9 });

  writeFileSync(`${path}.br`, brotli);
  writeFileSync(`${path}.gz`, gzip);

  const entry = {
    path: relativePath,
    size: raw.length,
    gzip: gzip.length,
    brotli: brotli.length,
    sha256: sha256(raw),
  };
  files.push(entry);

  rawTotal += raw.length;
  brTotal += brotli.length;
  gzTotal += gzip.length;

  const role = payloadRole(relativePath);
  if (role) {
    payload.uncompressed += raw.length;
    payload.gzip += gzip.length;
    payload.brotli += brotli.length;
    payload.files.push({ ...entry, role });
  }

  if (!quiet) {
    console.log(
      `  ${relativePath.padEnd(38)} ${(raw.length / 1024).toFixed(0).padStart(8)} KB` +
        ` -> gzip ${(gzip.length / 1024).toFixed(0).padStart(6)} KB` +
        ` / brotli ${(brotli.length / 1024).toFixed(0).padStart(6)} KB`,
    );
  }
}

// Deterministic order: the report is committed with the artifact hash and a
// re-run on the same player must produce the same file.
files.sort((left, right) => left.path.localeCompare(right.path));

const report = {
  label,
  producedAt: new Date().toISOString(),
  directory: dir,
  encoding: { brotli: { quality: 11, lgwin: 22 }, gzip: { level: 9 } },
  totals: { uncompressed: rawTotal, gzip: gzTotal, brotli: brTotal, fileCount: files.length },
  initialPayload: payload,
  files,
};

writeFileSync(join(dir, "compression-report.json"), `${JSON.stringify(report, null, 2)}\n`);

const mib = (bytes) => (bytes / (1024 * 1024)).toFixed(2);
console.log(
  `[compress-web] ${label}: initial payload ${mib(payload.uncompressed)} MiB raw, ` +
    `${mib(payload.gzip)} MiB gzip, ${mib(payload.brotli)} MiB Brotli`,
);
console.log(`[compress-web] wrote ${join(dir, "compression-report.json")}`);
