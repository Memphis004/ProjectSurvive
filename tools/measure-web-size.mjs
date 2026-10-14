#!/usr/bin/env node
/**
 * Measure a WebGL build's shipped payload (Stage 03b).
 *
 * Reports, for every file the browser actually downloads, its size
 * uncompressed and compressed with gzip and Brotli — and the total for the
 * whole build directory. Numbers are measured from the bytes on disk, never
 * estimated.
 *
 * Why compression is measured here instead of by toggling Unity's
 * "Compression Format": the build setting only decides whether Unity *emits*
 * pre-compressed siblings (`Web.wasm.br`); the compressed size of the payload
 * is the same either way. Measuring locally keeps one artifact per
 * configuration and makes the number reproducible from the raw files.
 *
 *   node tools/measure-web-size.mjs --dir UnityProject/Builds/Web-before --label before
 *   node tools/measure-web-size.mjs --dir <dir> --label x --json
 *   node tools/measure-web-size.mjs --dir <dir> --label x --json artifacts/size-x.json
 *   node tools/measure-web-size.mjs --dir <dir> --label x --markdown artifacts/size-x.md
 *
 * gzip is level 9 (`zlib.gzipSync`), Brotli is quality 11 / lgwin 22 — the
 * parameters are printed with the result, because a compressed size without
 * them is not comparable.
 *
 * Exit codes: 0 = measured, 1 = the directory has no player in it, 2 = bad usage.
 */

import { existsSync, readdirSync, readFileSync, statSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join, relative } from "node:path";
import { brotliCompressSync, constants as zlibConstants, gzipSync } from "node:zlib";

/**
 * The files a WebGL player loads, in load order. `{b}` is the player name, which
 * Unity derives from the output folder (Builds/Web-x -> Web-x.wasm), so it is
 * detected from the .wasm on disk instead of assumed.
 */
const PLAYER_FILE_PATTERNS = [
  "index.html",
  "Build/{b}.loader.js",
  "Build/{b}.framework.js",
  "Build/{b}.wasm",
  "Build/{b}.data",
];

/** Player name for `dir`, or the override. Empty when the directory has no player. */
function detectPlayerName(dir, override) {
  if (override) return override;
  const build = join(dir, "Build");
  if (!existsSync(build)) return "";
  const wasm = readdirSync(build).find((name) => name.endsWith(".wasm"));
  return wasm ? wasm.slice(0, -".wasm".length) : "";
}

const MIB = 1024 * 1024;

function parseArgs(argv) {
  const options = { dir: null, label: "build", json: false, jsonPath: null, markdown: null, quality: 11, gzipLevel: 9, base: null };
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === "--dir") options.dir = argv[++i];
    else if (arg === "--base") options.base = argv[++i];
    else if (arg === "--label") options.label = argv[++i];
    else if (arg === "--markdown") options.markdown = argv[++i];
    else if (arg === "--quality") options.quality = Number(argv[++i]);
    else if (arg === "--gzip-level") options.gzipLevel = Number(argv[++i]);
    else if (arg === "--json") {
      // `--json` prints, `--json <path>` also writes - the path form is what pipeline
      // steps use, and it must not swallow the next flag when no path is given.
      const next = argv[i + 1];
      if (next !== undefined && !next.startsWith("--")) options.jsonPath = argv[++i];
      else options.json = true;
    }
    else if (arg === "--help" || arg === "-h") options.help = true;
    else {
      console.error(`unknown argument: ${arg}`);
      process.exit(2);
    }
  }
  return options;
}

/** Every file under `dir`, with its size, largest first. */
function walk(dir) {
  const found = [];
  const stack = [dir];
  while (stack.length > 0) {
    const current = stack.pop();
    for (const entry of readdirSync(current, { withFileTypes: true })) {
      const path = join(current, entry.name);
      if (entry.isDirectory()) stack.push(path);
      else if (entry.isFile()) found.push({ path, size: statSync(path).size });
    }
  }
  return found.sort((a, b) => b.size - a.size);
}

function measure(path, options) {
  const raw = readFileSync(path);
  const gzip = gzipSync(raw, { level: options.gzipLevel });
  const brotli = brotliCompressSync(raw, {
    params: { [zlibConstants.BROTLI_PARAM_QUALITY]: options.quality, [zlibConstants.BROTLI_PARAM_LGWIN]: 22 },
  });
  return { uncompressed: raw.length, gzip: gzip.length, brotli: brotli.length };
}

const mib = (bytes) => (bytes / MIB).toFixed(2);
const percent = (part, whole) => (whole === 0 ? "0.0" : ((part / whole) * 100).toFixed(1));

const options = parseArgs(process.argv.slice(2));
if (options.help || !options.dir) {
  console.error("usage: node tools/measure-web-size.mjs --dir <build-dir> [--label name] [--json [out.json]] [--markdown out.md] [--base <dir>]");
  process.exit(options.help ? 0 : 2);
}

if (!existsSync(options.dir)) {
  console.error(`no such directory: ${options.dir}`);
  process.exit(1);
}

const all = walk(options.dir);
if (all.length === 0) {
  console.error(`${options.dir} is empty - nothing to measure`);
  process.exit(1);
}

const playerName = detectPlayerName(options.dir, options.base);
const byRelative = new Map(all.map((file) => [relative(options.dir, file.path).replaceAll("\\", "/"), file]));
const rows = [];
for (const pattern of PLAYER_FILE_PATTERNS) {
  const name = pattern.replace("{b}", playerName);
  const file = byRelative.get(name);
  if (!file) {
    rows.push({ file: name, missing: true });
    continue;
  }
  const started = Date.now();
  const sizes = measure(file.path, options);
  rows.push({ file: name, ...sizes, ms: Date.now() - started });
}

if (rows.every((row) => row.missing)) {
  console.error(`${options.dir} does not look like a WebGL player (no index.html / Build/*.wasm found)`);
  process.exit(1);
}

const shipped = rows.filter((row) => !row.missing);
const totalUncompressed = shipped.reduce((sum, row) => sum + row.uncompressed, 0);
const totalGzip = shipped.reduce((sum, row) => sum + row.gzip, 0);
const totalBrotli = shipped.reduce((sum, row) => sum + row.brotli, 0);
const allBytes = all.reduce((sum, file) => sum + file.size, 0);

const lines = [];
lines.push(`### ${options.label}`);
lines.push("");
lines.push("| File | Uncompressed | gzip -9 | Brotli q11 | Brotli % of raw |");
lines.push("| --- | ---: | ---: | ---: | ---: |");
for (const row of rows) {
  if (row.missing) {
    lines.push(`| \`${row.file}\` | — | — | — | missing |`);
    continue;
  }
  lines.push(
    `| \`${row.file}\` | ${row.uncompressed.toLocaleString("en-US")} B (${mib(row.uncompressed)} MiB) | ` +
      `${row.gzip.toLocaleString("en-US")} B (${mib(row.gzip)} MiB) | ` +
      `${row.brotli.toLocaleString("en-US")} B (${mib(row.brotli)} MiB) | ` +
      `${percent(row.brotli, row.uncompressed)} % |`,
  );
}
lines.push(
  `| **player total** | **${totalUncompressed.toLocaleString("en-US")} B (${mib(totalUncompressed)} MiB)** | ` +
    `**${mib(totalGzip)} MiB** | **${mib(totalBrotli)} MiB** | **${percent(totalBrotli, totalUncompressed)} %** |`,
);
lines.push(`| whole build directory | ${allBytes.toLocaleString("en-US")} B (${mib(allBytes)} MiB) | | | |`);
lines.push("");
lines.push(
  `Largest files in the directory: ${all
    .slice(0, 6)
    .map((file) => `\`${relative(options.dir, file.path).replaceAll("\\", "/")}\` ${mib(file.size)} MiB`)
    .join(", ")}`,
);

const markdown = lines.join("\n");
const json = JSON.stringify(
      {
        label: options.label,
        directory: options.dir,
        playerName,
        gzipLevel: options.gzipLevel,
        brotliQuality: options.quality,
        rows: rows.filter((row) => !row.missing),
        totals: {
          playerUncompressed: totalUncompressed,
          playerGzip: totalGzip,
          playerBrotli: totalBrotli,
          buildDirectory: allBytes,
        },
        largestFiles: all.slice(0, 20).map((file) => ({
          path: relative(options.dir, file.path).replaceAll("\\", "/"),
          bytes: file.size,
        })),
      },
      null,
      2,
);

if (options.json) console.log(json);
else console.log(markdown);

if (options.jsonPath) {
  mkdirSync(dirname(options.jsonPath), { recursive: true });
  writeFileSync(options.jsonPath, json + "\n", "utf8");
  console.error(`wrote ${options.jsonPath}`);
}

if (options.markdown) {
  mkdirSync(dirname(options.markdown), { recursive: true });
  writeFileSync(options.markdown, markdown + "\n", "utf8");
  // stderr, so `--json > file.json` stays valid JSON.
  console.error(`wrote ${options.markdown}`);
}
