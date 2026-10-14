#!/usr/bin/env node
/**
 * Attribute a WebGL build's IL2CPP code to the assembly it came from (Stage 03b).
 *
 * The Unity Build Report ranks packed *assets*; it says nothing about which
 * managed assembly's code ended up in the `.wasm`. IL2CPP leaves that evidence on
 * disk and this reads it:
 *
 *   <artifacts>/il2cppOutput/cpp/<Assembly>*.cpp   one C++ translation unit per
 *                                                  assembly (Unityiles them into
 *                                                  `__N` parts and a `_CodeGen.c`)
 *   <artifacts>/ManagedStripped/<Assembly>.dll     the managed assembly *after*
 *                                                  managed stripping
 *   <artifacts>/il2cppOutput/data/Metadata/global-metadata.dat
 *
 * Generated C++ bytes are a proxy, not the answer: the linker then drops
 * unreachable sections, and `-Os`/`OptimizeSize` compress what remains. They are
 * still the only per-assembly signal a build emits, they are stable enough to
 * rank assemblies against each other, and the ranking is what the size work needs
 * ("which assembly's code is in here"). The report says which figure is which.
 *
 * Buckets that are not one assembly are kept out of the ranking and named:
 * `Generics__N` / `GenericMethods__N` are IL2CPP's shared generic instantiations
 * (they belong to whichever assembly instantiated them first, and IL2CPP does not
 * record which), and `Il2Cpp*` is the runtime's own tables and stubs.
 *
 *   node tools/measure-il2cpp-size.mjs --artifacts UnityProject/Library/Bee/artifacts/WebGL --label strip-high --json out.json
 *
 * Exit codes: 0 = measured, 1 = nothing to measure, 2 = bad usage.
 */

import { existsSync, readdirSync, statSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";

const MIB = 1024 * 1024;

/** IL2CPP output that is infrastructure rather than one assembly's code. */
const INFRASTRUCTURE = [
  { match: /^Generics(__\d+)?\.cpp$/, bucket: "shared generic instantiations" },
  { match: /^GenericMethods(__\d+)?\.cpp$/, bucket: "shared generic methods" },
  { match: /^Il2Cpp/, bucket: "IL2CPP runtime tables and stubs" },
];

function parseArgs(argv) {
  const options = { artifacts: null, label: "build", json: null, markdown: null, top: 20 };
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === "--artifacts") options.artifacts = argv[++i];
    else if (arg === "--label") options.label = argv[++i];
    else if (arg === "--json") options.json = argv[++i];
    else if (arg === "--markdown") options.markdown = argv[++i];
    else if (arg === "--top") options.top = Number(argv[++i]);
    else if (arg === "--help" || arg === "-h") options.help = true;
    else {
      console.error(`unknown argument: ${arg}`);
      process.exit(2);
    }
  }
  return options;
}

/**
 * The assembly a generated file belongs to: `Google.Protobuf__2.cpp` and
 * `Google.Protobuf_CodeGen.c` are both `Google.Protobuf`. A generated file whose
 * name has no assembly part is attributed to `(unattributed)` rather than dropped,
 * so the totals stay honest.
 */
function assemblyOf(fileName) {
  const base = fileName.replace(/_CodeGen\.c$/, "").replace(/\.(cpp|c)$/, "");
  const withoutPart = base.replace(/__\d+$/, "");
  return withoutPart.length === 0 ? "(unattributed)" : withoutPart;
}

const options = parseArgs(process.argv.slice(2));
if (options.help || !options.artifacts) {
  console.error("usage: node tools/measure-il2cpp-size.mjs --artifacts <Library/Bee/artifacts/WebGL> [--label name] [--json out.json] [--markdown out.md]");
  process.exit(options.help ? 0 : 2);
}

const cppDir = join(options.artifacts, "il2cppOutput", "cpp");
if (!existsSync(cppDir)) {
  console.error(`no IL2CPP generated code at ${cppDir} - build first, or pass the right --artifacts`);
  process.exit(1);
}

const byAssembly = new Map();
const byBucket = new Map();
let generatedTotal = 0;

for (const entry of readdirSync(cppDir, { withFileTypes: true })) {
  if (!entry.isFile()) continue;
  if (!/\.(cpp|c)$/.test(entry.name)) continue;

  const bytes = statSync(join(cppDir, entry.name)).size;
  generatedTotal += bytes;

  const bucket = INFRASTRUCTURE.find((rule) => rule.match.test(entry.name));
  if (bucket) {
    byBucket.set(bucket.bucket, (byBucket.get(bucket.bucket) ?? 0) + bytes);
    continue;
  }

  const assembly = assemblyOf(entry.name);
  const current = byAssembly.get(assembly) ?? { bytes: 0, files: 0 };
  byAssembly.set(assembly, { bytes: current.bytes + bytes, files: current.files + 1 });
}

/** Managed assemblies that survived stripping, by size. */
const managedStripped = new Map();
const strippedDir = join(options.artifacts, "ManagedStripped");
if (existsSync(strippedDir)) {
  for (const entry of readdirSync(strippedDir, { withFileTypes: true })) {
    if (entry.isFile() && entry.name.endsWith(".dll")) {
      managedStripped.set(entry.name.replace(/\.dll$/, ""), statSync(join(strippedDir, entry.name)).size);
    }
  }
}

const metadataPath = join(options.artifacts, "il2cppOutput", "data", "Metadata", "global-metadata.dat");
const metadataBytes = existsSync(metadataPath) ? statSync(metadataPath).size : null;

const assemblies = [...byAssembly.entries()]
  .map(([name, value]) => ({
    assembly: name,
    generatedCppBytes: value.bytes,
    generatedCppFiles: value.files,
    strippedManagedBytes: managedStripped.get(name) ?? null,
    shareOfGeneratedPercent: Number(((value.bytes / generatedTotal) * 100).toFixed(2)),
  }))
  .sort((a, b) => b.generatedCppBytes - a.generatedCppBytes);

const buckets = [...byBucket.entries()]
  .map(([bucket, bytes]) => ({ bucket, generatedCppBytes: bytes, shareOfGeneratedPercent: Number(((bytes / generatedTotal) * 100).toFixed(2)) }))
  .sort((a, b) => b.generatedCppBytes - a.generatedCppBytes);

const result = {
  label: options.label,
  artifactsDirectory: options.artifacts,
  generatedCppTotalBytes: generatedTotal,
  generatedCppFileCount: assemblies.reduce((sum, a) => sum + a.generatedCppFiles, 0),
  globalMetadataBytes: metadataBytes,
  infrastructureBuckets: buckets,
  assemblyCount: assemblies.length,
  assemblies,
};

const mib = (bytes) => (bytes / MIB).toFixed(2);
const rows = assemblies.slice(0, options.top);
const lines = [];
lines.push(`### IL2CPP code by assembly — ${options.label}`);
lines.push("");
lines.push(`Generated C++ total: ${generatedTotal.toLocaleString("en-US")} B (${mib(generatedTotal)} MiB) across ${result.generatedCppFileCount} files, ${assemblies.length} assemblies.`);
if (metadataBytes) lines.push(`global-metadata.dat: ${metadataBytes.toLocaleString("en-US")} B (${mib(metadataBytes)} MiB).`);
lines.push("");
lines.push(`Top ${rows.length} assemblies by generated C++ bytes:`);
lines.push("");
lines.push("| # | Assembly | Generated C++ B | MiB | % of all | Stripped managed B | Files |");
lines.push("| ---: | --- | ---: | ---: | ---: | ---: | ---: |");
rows.forEach((row, index) => {
  lines.push(
    `| ${index + 1} | \`${row.assembly}\` | ${row.generatedCppBytes.toLocaleString("en-US")} | ${mib(row.generatedCppBytes)} | ${row.shareOfGeneratedPercent} % | ` +
      `${row.strippedManagedBytes === null ? "—" : row.strippedManagedBytes.toLocaleString("en-US")} | ${row.generatedCppFiles} |`,
  );
});
lines.push("");
lines.push("Not one assembly (kept out of the ranking above):");
lines.push("");
lines.push("| Bucket | Generated C++ B | MiB | % of all |");
lines.push("| --- | ---: | ---: | ---: |");
for (const bucket of buckets) {
  lines.push(`| ${bucket.bucket} | ${bucket.generatedCppBytes.toLocaleString("en-US")} | ${mib(bucket.generatedCppBytes)} | ${bucket.shareOfGeneratedPercent} % |`);
}
const infrastructureBytes = buckets.reduce((sum, b) => sum + b.generatedCppBytes, 0);
lines.push(`| **not attributable to one assembly** | **${infrastructureBytes.toLocaleString("en-US")}** | **${mib(infrastructureBytes)}** | **${((infrastructureBytes / generatedTotal) * 100).toFixed(2)} %** |`);

const markdown = lines.join("\n");
console.log(markdown);

if (options.json) {
  mkdirSync(dirname(options.json), { recursive: true });
  writeFileSync(options.json, JSON.stringify(result, null, 2) + "\n", "utf8");
  console.error(`wrote ${options.json}`);
}
if (options.markdown) {
  mkdirSync(dirname(options.markdown), { recursive: true });
  writeFileSync(options.markdown, markdown + "\n", "utf8");
  console.error(`wrote ${options.markdown}`);
}
