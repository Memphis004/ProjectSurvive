#!/usr/bin/env node
/**
 * Check that every relative markdown link under docs/ and llmwiki/ resolves.
 *
 * Why this exists: the template cut deleted a game's design document and its stage
 * reports, and moved its ADRs and system notes into docs/template/examples/. A link
 * that still points at a deleted file is the fastest way for an agent to trust a
 * document that no longer describes the repository.
 *
 * What it checks:
 *   - inline links and images: [text](path) / ![alt](path)
 *   - reference definitions: [id]: path
 *   - the path resolves to a file (or a directory) that exists, with matching case
 *   - the target is **in the repository** — a file git tracks, or an untracked file git
 *     would add (`--others --exclude-standard`), so a document added in the same commit
 *     as its link still passes. What fails is a target only ignored output has:
 *     `artifacts/`, `UnityProject/Builds/`, `node_modules/` resolve on the machine that
 *     produced them and are broken in every clone, which is a false PASS this check
 *     exists to prevent
 *
 * What it does NOT check:
 *   - URLs (http, https, mailto)
 *   - anchors within a file (`#section`) — the path is checked, the anchor is not
 *   - links inside fenced code blocks or inline code
 *   - docs/template/examples/**, which is an archive of the game's documents and is
 *     allowed to keep its original links
 *
 * Usage:
 *   node tools/check-links.mjs            # check, exit 1 on any broken link
 *   node tools/check-links.mjs --verbose  # also print every checked file
 *
 * Exit codes follow the repository's convention:
 *   0 = every link resolves
 *   1 = at least one link is broken
 *   2 = the checker itself could not run
 */

import { execFileSync } from "node:child_process";
import { readFileSync, readdirSync, statSync } from "node:fs";
import { dirname, join, parse, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const verbose = process.argv.includes("--verbose");

/** Roots to walk, and directories under them that are archives. */
const roots = ["docs", "llmwiki"];
const rootFiles = ["AGENTS.md", "knowledge.md"];
const excludedDirs = ["docs/template/examples"];

function normalize(p) {
  return p.split(sep).join("/");
}

function isExcluded(relPath) {
  return excludedDirs.some(
    (dir) => relPath === dir || relPath.startsWith(dir + "/"),
  );
}

function walk(dir, out) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    if (entry.name === "node_modules" || entry.name === ".git") continue;
    const full = join(dir, entry.name);
    const rel = normalize(relative(repoRoot, full));
    if (isExcluded(rel)) continue;
    if (entry.isDirectory()) walk(full, out);
    else if (entry.isFile() && entry.name.toLowerCase().endsWith(".md")) out.push(full);
  }
}

/** Remove fenced code blocks and inline code spans, keeping line numbers intact. */
function stripCode(text) {
  const lines = text.split(/\r?\n/);
  const out = [];
  let fence = null;
  for (const line of lines) {
    const match = line.match(/^\s*(```+|~~~+)/);
    if (match) {
      if (fence === null) fence = match[1][0];
      else if (match[1][0] === fence) fence = null;
      out.push("");
      continue;
    }
    if (fence !== null) {
      out.push("");
      continue;
    }
    out.push(line.replace(/`[^`]*`/g, (span) => " ".repeat(span.length)));
  }
  return out;
}

/** All candidate link targets in one markdown file, with their 1-based line. */
function collectTargets(text) {
  const lines = stripCode(text);
  const targets = [];

  // [text](path "title") and ![alt](path) — the path may contain balanced parens.
  const inline = /!?\[[^\]]*\]\(\s*(<[^>]*>|[^)\s]+)(?:\s+"[^"]*")?\s*\)/g;
  // [id]: path
  const reference = /^\s*\[[^\]]+\]:\s*(<[^>]*>|\S+)/;

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    let m;
    inline.lastIndex = 0;
    while ((m = inline.exec(line)) !== null) {
      targets.push({ target: m[1], line: i + 1 });
    }
    const ref = line.match(reference);
    if (ref) targets.push({ target: ref[1], line: i + 1 });
  }

  return targets;
}

function isExternal(target) {
  return (
    target === "" ||
    target.startsWith("#") ||
    target.startsWith("/") ||
    /^[a-zA-Z][a-zA-Z0-9+.-]*:/.test(target)
  );
}

function cleanTarget(raw) {
  let target = raw.trim();
  if (target.startsWith("<") && target.endsWith(">")) target = target.slice(1, -1);
  const hash = target.indexOf("#");
  if (hash >= 0) target = target.slice(0, hash);
  const query = target.indexOf("?");
  if (query >= 0) target = target.slice(0, query);
  try {
    target = decodeURIComponent(target);
  } catch {
    // A malformed escape is the link's problem; the existence check will report it.
  }
  return target;
}

/**
 * True when every path component exists with exactly this casing.
 * Windows would answer `exists` for the wrong case; a case-sensitive CI would not.
 */
function existsWithCase(absolutePath) {
  const full = resolve(absolutePath);
  if (!statSync(full, { throwIfNoEntry: false })) return false;

  const { root } = parse(full);
  const parts = normalize(full.slice(root.length))
    .split("/")
    .filter(Boolean);
  let current = root;
  for (const part of parts) {
    if (!readdirSync(current).includes(part)) return false;
    current = join(current, part);
  }
  return true;
}

/**
 * Every path a clone would have - tracked files, plus untracked ones that are not ignored -
 * or null when git cannot answer (a tarball export, no git installed). In that case the
 * "is it in the repository" half of the check is reported as skipped rather than guessed at.
 */
function trackedPaths() {
  try {
    return new Set(
      execFileSync("git", ["ls-files", "-z", "--cached", "--others", "--exclude-standard"], {
        cwd: repoRoot,
        encoding: "utf8",
        maxBuffer: 64 * 1024 * 1024,
      })
        .split("\0")
        .filter(Boolean),
    );
  } catch {
    return null;
  }
}

/** True when git tracks the file, or at least one file under the directory. */
function isTracked(relPath, tracked) {
  if (tracked.has(relPath)) return true;
  const prefix = `${relPath.replace(/\/$/, "")}/`;
  for (const path of tracked) {
    if (path.startsWith(prefix)) return true;
  }
  return false;
}

function main() {
  const tracked = trackedPaths();
  const files = [];
  for (const root of roots) {
    const absolute = join(repoRoot, root);
    try {
      if (statSync(absolute).isDirectory()) walk(absolute, files);
    } catch {
      throw new Error(`missing directory: ${root}`);
    }
  }
  for (const file of rootFiles) {
    try {
      if (statSync(join(repoRoot, file)).isFile()) files.push(join(repoRoot, file));
    } catch {
      // A root file may legitimately not exist; nothing to check.
    }
  }

  const problems = [];
  let checked = 0;

  for (const file of files) {
    const text = readFileSync(file, "utf8");
    const relFile = normalize(relative(repoRoot, file));
    if (verbose) console.log(`checking ${relFile}`);

    for (const { target, line } of collectTargets(text)) {
      if (isExternal(target)) continue;
      const cleaned = cleanTarget(target);
      if (cleaned === "") continue;
      checked++;

      const absolute = resolve(dirname(file), cleaned);
      const relTarget = normalize(relative(repoRoot, absolute));
      if (!existsWithCase(absolute)) {
        problems.push({ file: relFile, line, target, resolved: relTarget, why: "missing" });
        continue;
      }
      if (tracked && (relTarget.startsWith("../") || !isTracked(relTarget, tracked))) {
        problems.push({ file: relFile, line, target, resolved: relTarget, why: "not in the repository" });
      }
    }
  }

  for (const p of problems) {
    console.log(`${p.file}:${p.line}  ${p.target}  ->  ${p.resolved} (${p.why})`);
  }

  console.log(
    `\n${checked} link(s) in ${files.length} file(s): ` +
      (problems.length === 0 ? "all resolve, and every target is in the repository" : `${problems.length} BROKEN`),
  );
  if (tracked === null) {
    console.log("git could not list repository paths, so ignored targets were not checked.");
  }
  if (problems.length > 0) {
    console.log(
      "Fix the link, or move the target back into the live tree. A target that only " +
        "exists in ignored output (artifacts/, Builds/, node_modules/) is broken in every " +
        "clone, and docs/template/examples/ is excluded because it is an archive.",
    );
    return 1;
  }
  return 0;
}

try {
  process.exitCode = main();
} catch (error) {
  console.error(`check-links could not run: ${error.message}`);
  process.exitCode = 2;
}
