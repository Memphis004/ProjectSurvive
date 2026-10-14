#!/usr/bin/env node
// Per-file line coverage from a Cobertura report, so the Stage 05 acceptance
// criterion ("coverage of the backpack rules >= 85 %") is a measurement that
// anyone can re-run rather than a number copied out of a UI.
//
// The XPlat collector by default does not instrument every referenced assembly,
// so the run must pass --settings lib/Tests~/coverage-domain.runsettings (it
// pins the Domain assembly). See docs/stages/05-report.md for the exact command.
//
// Usage:
//   node tools/coverage-report.mjs <coverage.cobertura.xml> [path-substring...]
//
// With no substring it prints every file. With one or more substrings (matched
// case-insensitively against the file path) it prints those files plus an
// aggregate over the union, which is the number the acceptance criterion reads.

import { readFileSync } from "node:fs";

const [reportPath, ...filters] = process.argv.slice(2);
if (!reportPath) {
  console.error("usage: node tools/coverage-report.mjs <coverage.cobertura.xml> [path-substring...]");
  process.exit(2);
}

const xml = readFileSync(reportPath, "utf8");

// One entry per <class ...> element: its file and the lines it owns.
// A line shared by two classes in one file (nested types) is counted once,
// keyed by "file:lineNumber".
const perFile = new Map();
const seen = new Set();
const classTag = /<class\s+name="([^"]*)"\s+filename="([^"]*)"[^>]*>([\s\S]*?)<\/class>/g;

for (const classMatch of xml.matchAll(classTag)) {
  const file = classMatch[2].replace(/\\/g, "/");
  const body = classMatch[3];
  if (!perFile.has(file)) {
    perFile.set(file, { lines: new Map() });
  }
  const entry = perFile.get(file);

  for (const lineMatch of body.matchAll(/<line\s+number="(\d+)"\s+hits="(\d+)"/g)) {
    const key = file + ":" + lineMatch[1];
    if (seen.has(key)) {
      continue;
    }
    seen.add(key);
    entry.lines.set(Number(lineMatch[1]), Number(lineMatch[2]));
  }
}

if (perFile.size === 0) {
  console.error("no <class> elements found in " + reportPath + " - wrong report or empty run?");
  process.exit(1);
}

const rows = [...perFile.entries()].map(([file, entry]) => {
  const total = entry.lines.size;
  let covered = 0;
  for (const hits of entry.lines.values()) {
    if (hits > 0) {
      covered++;
    }
  }
  return { file, covered, total, rate: total === 0 ? 1 : covered / total };
});

rows.sort((a, b) => a.file.localeCompare(b.file));

const lower = filters.map((f) => f.toLowerCase());
const selected = lower.length === 0
  ? rows
  : rows.filter((row) => lower.some((f) => row.file.toLowerCase().includes(f)));

const width = Math.max(...selected.map((row) => row.file.length), 12);
const percent = (rate) => (rate * 100).toFixed(2).padStart(6) + "%";

console.log("file".padEnd(width) + "  lines        coverage");
console.log("-".repeat(width + 24));
for (const row of selected) {
  console.log(
    row.file.padEnd(width) + "  " + `${row.covered}/${row.total}`.padStart(11) + "  " + percent(row.rate),
  );
}

if (selected.length !== rows.length || selected.length > 1) {
  const covered = selected.reduce((sum, row) => sum + row.covered, 0);
  const total = selected.reduce((sum, row) => sum + row.total, 0);
  console.log("-".repeat(width + 24));
  console.log(
    `AGGREGATE (${selected.length} files)`.padEnd(width) +
      "  " + `${covered}/${total}`.padStart(11) + "  " + percent(total === 0 ? 1 : covered / total),
  );
}
