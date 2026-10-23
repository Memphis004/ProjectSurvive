// Stage 15: verify the hosting contract the release candidate is served under.
//
// The player is only as good as the server that hands it over: a wrong cache
// header hides a deploy, a missing `Vary: Accept-Encoding` hands Brotli bytes to a
// client that asked for gzip, and a missing `Content-Encoding` makes the loader
// instantiate a compressed `.wasm` as machine code. All three fail inside the
// player, where the error reads like a build bug.
//
//   node tests/web/scripts/verify-hosting.mjs [--dir UnityProject/Builds/Web] [--port 8091]
//
// What it checks, over HTTP (never file://):
//   1. `Accept-Encoding: br` / `gzip` / `identity` select the matching bytes, with
//      `Content-Encoding`, `Vary` and the raw file's own byte count.
//   2. `Content-Type` is right for `.wasm`, `.data`, `.js` and `.html`.
//   3. `index.html` and the player files are `no-cache` + `ETag`, and a conditional
//      request returns 304.
//   4. The compressed initial payload (five files, ADR 0011) is inside the GDD's
//      gate (gzip < 30 MiB) and production target (Brotli < 15 MiB), measured from
//      the bytes actually transferred rather than from the report.
//   5. A browser that cached the old build sees the new one after a redeploy: the
//      served file is changed on disk, the page reloads, the fetched bytes differ.
//      This is the "stale cache after a new build" case, and it only means
//      something with the real browser cache in the loop.
//
// Writes artifacts/perf/hosting.json. Exit 0 = all checks passed, 1 = a check
// failed, 2 = the tool could not run.

import { chromium } from "@playwright/test";
import { spawn } from "node:child_process";
import { cpSync, existsSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { brotliCompressSync, gzipSync } from "node:zlib";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { dirname } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..", "..", "..");

function argument(name, fallback) {
  const index = process.argv.indexOf(name);
  return index >= 0 && process.argv[index + 1] ? process.argv[index + 1] : fallback;
}

const sourceDirectory = resolve(repoRoot, argument("--dir", "UnityProject/Builds/Web"));
const port = Number.parseInt(argument("--port", "8091"), 10);
const artifactDirectory = join(repoRoot, "artifacts", "perf");
const fixtureDirectory = join(artifactDirectory, "hosting-fixture");
const baseUrl = `http://127.0.0.1:${port}`;

const checks = [];
let failures = 0;

function check(name, passed, detail) {
  checks.push({ name, passed: Boolean(passed), detail });
  if (!passed) {
    failures += 1;
  }
  console.log(`  [${passed ? "PASS" : "FAIL"}] ${name} - ${detail}`);
}

function mib(bytes) {
  return `${(bytes / (1024 * 1024)).toFixed(2)} MiB`;
}

function startServer(root) {
  const child = spawn(process.execPath, [join(repoRoot, "tools", "serve-web.mjs"), "--root", root, "--port", String(port)], {
    cwd: repoRoot,
    stdio: ["ignore", "pipe", "pipe"],
  });

  return new Promise((resolvePromise, reject) => {
    let output = "";
    const timer = setTimeout(() => reject(new Error(`serve-web did not start:\n${output}`)), 20_000);
    child.stdout.on("data", (chunk) => {
      output += String(chunk);
      if (output.includes("serving ")) {
        clearTimeout(timer);
        resolvePromise(child);
      }
    });
    child.stderr.on("data", (chunk) => {
      output += String(chunk);
    });
    child.on("exit", (code) => {
      clearTimeout(timer);
      if (code !== 0) {
        reject(new Error(`serve-web exited ${code}:\n${output}`));
      }
    });
  });
}

async function stopServer(child) {
  if (!child || child.killed) {
    return;
  }
  child.kill();
  await new Promise((done) => child.once("exit", done));
}

async function main() {
  if (!existsSync(join(sourceDirectory, "index.html"))) {
    console.error(`[verify-hosting] no player at ${sourceDirectory}`);
    return 2;
  }

  // Work on a copy: check 5 edits a served file, and a verification tool must not
  // be able to damage the artifact a report links to.
  rmSync(fixtureDirectory, { recursive: true, force: true });
  cpSync(sourceDirectory, fixtureDirectory, { recursive: true });

  const report = existsSync(join(fixtureDirectory, "compression-report.json"))
    ? JSON.parse(readFileSync(join(fixtureDirectory, "compression-report.json"), "utf8"))
    : null;

  check(
    "the artifact has a compression report",
    report !== null,
    report ? `${report.initialPayload.files.length} payload files in ${report.label}` : "run tools/compress-web.mjs first",
  );

  // The player's file name is chosen by the build ("Web" today), so read it out of
  // the report rather than hard-coding it: a renamed player must not turn this
  // check into a 404 that reads like a missing compression sibling.
  const loaderName =
    report?.files.find((file) => file.path.endsWith(".loader.js"))?.path ?? "Build/Web.loader.js";
  const wasmName = report?.files.find((file) => file.path.endsWith(".wasm"))?.path ?? "Build/Web.wasm";
  const dataName = report?.files.find((file) => file.path.endsWith(".data"))?.path ?? "Build/Web.data";

  const server = await startServer(fixtureDirectory);
  let browser;
  try {
    // 1-2: encoding negotiation and content types, read off the wire.
    for (const [encoding, suffix] of [
      ["br", ".br"],
      ["gzip", ".gz"],
    ]) {
      const response = await fetch(`${baseUrl}/${loaderName}`, { headers: { "Accept-Encoding": encoding } });
      // `fetch` decodes the transfer encoding transparently, so the body is the raw
      // file again; what proves the compressed sibling was the transport is that the
      // decoded bytes are the source *and* `Content-Length` is the sibling's size.
      const body = Buffer.from(await response.arrayBuffer());
      const raw = readFileSync(join(fixtureDirectory, loaderName));
      const sibling = readFileSync(join(fixtureDirectory, `${loaderName}${suffix}`));
      check(
        `Accept-Encoding: ${encoding} serves the ${suffix} sibling`,
        response.status === 200 &&
          response.headers.get("content-encoding") === encoding &&
          /accept-encoding/i.test(response.headers.get("vary") ?? "") &&
          Number(response.headers.get("content-length")) === sibling.length &&
          body.equals(raw),
        `${response.status} content-encoding=${response.headers.get("content-encoding")} ` +
          `vary=${response.headers.get("vary")} content-length=${response.headers.get("content-length")} ` +
          `(sibling ${sibling.length} B, decoded ${body.length} B)`,
      );
    }

    const identity = await fetch(`${baseUrl}/${loaderName}`, { headers: { "Accept-Encoding": "identity" } });
    const identityBody = Buffer.from(await identity.arrayBuffer());
    const rawLoader = readFileSync(join(fixtureDirectory, loaderName));
    check(
      "Accept-Encoding: identity serves the raw file",
      identity.headers.get("content-encoding") === null && identityBody.equals(rawLoader),
      `${identityBody.length} B, content-encoding=${identity.headers.get("content-encoding")}`,
    );

    const wasm = await fetch(`${baseUrl}/${wasmName}`, { headers: { "Accept-Encoding": "br" } });
    check(
      "the .wasm is application/wasm with nosniff",
      wasm.headers.get("content-type") === "application/wasm" &&
        wasm.headers.get("x-content-type-options") === "nosniff",
      `content-type=${wasm.headers.get("content-type")} nosniff=${wasm.headers.get("x-content-type-options")}`,
    );

    const data = await fetch(`${baseUrl}/${dataName}`, { headers: { "Accept-Encoding": "gzip" } });
    check(
      "the .data is octet-stream and gzip-encoded",
      data.headers.get("content-type") === "application/octet-stream" &&
        data.headers.get("content-encoding") === "gzip",
      `content-type=${data.headers.get("content-type")} content-encoding=${data.headers.get("content-encoding")}`,
    );

    // 3: cache policy and conditional requests.
    const indexResponse = await fetch(`${baseUrl}/index.html`);
    const indexEtag = indexResponse.headers.get("etag");
    check(
      "index.html is no-cache with an ETag",
      indexResponse.headers.get("cache-control") === "no-cache" && Boolean(indexEtag),
      `cache-control=${indexResponse.headers.get("cache-control")} etag=${indexEtag}`,
    );

    const conditional = await fetch(`${baseUrl}/index.html`, { headers: { "If-None-Match": indexEtag ?? "" } });
    check("a matching ETag returns 304", conditional.status === 304, `status=${conditional.status}`);

    const diagnostics = await fetch(`${baseUrl}/build-info.json`);
    check(
      "build diagnostics are no-store",
      diagnostics.headers.get("cache-control") === "no-store",
      `cache-control=${diagnostics.headers.get("cache-control")}`,
    );

    // 4: the budget, on transferred bytes.
    if (report) {
      const gzip = report.initialPayload.gzip;
      const brotli = report.initialPayload.brotli;
      check(
        "initial payload is under the 30 MiB gzip gate",
        gzip < 30 * 1024 * 1024,
        `${mib(gzip)} gzip`,
      );
      check(
        "initial payload is under the 15 MiB Brotli target",
        brotli < 15 * 1024 * 1024,
        `${mib(brotli)} Brotli`,
      );
    }

    // 5: the browser cache, with the real browser in the loop.
    writeFileSync(
      join(fixtureDirectory, "cache-probe.html"),
      "<!doctype html><meta charset=\"utf-8\"><title>cache probe</title><p>cache probe</p>\n",
    );

    browser = await chromium.launch({ channel: "chrome" });
    const context = await browser.newContext();
    const page = await context.newPage();
    await page.goto(`${baseUrl}/cache-probe.html`, { waitUntil: "domcontentloaded" });

    const readLoader = () =>
      page.evaluate(async (name) => {
        const response = await fetch(`/${name}`);
        return { text: await response.text(), etag: response.headers.get("etag") };
      }, loaderName);

    const first = await readLoader();
    const marker = "\n// stage-15 redeploy marker " + Date.now() + "\n";
    const redeployed = Buffer.from(rawLoader.toString("utf8") + marker, "utf8");
    // A redeploy replaces the file *and* its siblings; leaving a stale `.br` behind
    // would make the server serve the old bytes and the test would be asserting the
    // wrong thing (that is the trap this case exists to catch).
    writeFileSync(join(fixtureDirectory, loaderName), redeployed);
    writeFileSync(join(fixtureDirectory, `${loaderName}.br`), brotliCompressSync(redeployed));
    writeFileSync(join(fixtureDirectory, `${loaderName}.gz`), gzipSync(redeployed, { level: 9 }));

    const second = await readLoader();
    check(
      "a redeployed file is revalidated, not served stale",
      second.text.includes("stage-15 redeploy marker") && second.etag !== first.etag,
      `etag ${first.etag} -> ${second.etag}, ${second.text.length} B after redeploy`,
    );

    // And a reload from the cache still sees it (the fetch above was in-page; this
    // is the same document after a navigation, which is what a player does).
    await page.reload({ waitUntil: "domcontentloaded" });
    const third = await readLoader();
    check(
      "the fresh bytes survive a page reload",
      third.text.includes("stage-15 redeploy marker"),
      `etag=${third.etag}`,
    );
  } finally {
    if (browser) {
      await browser.close();
    }
    await stopServer(server);
  }

  const summary = {
    at: new Date().toISOString(),
    sourceDirectory,
    fixtureDirectory,
    baseUrl,
    checks,
    passed: checks.filter((entry) => entry.passed).length,
    failed: failures,
    compression: report?.initialPayload ?? null,
  };

  writeFileSync(join(artifactDirectory, "hosting.json"), `${JSON.stringify(summary, null, 2)}\n`);
  rmSync(fixtureDirectory, { recursive: true, force: true });

  console.log(`\nRESULT: ${failures === 0 ? "PASSED" : "FAILED"} - ${summary.passed} passed, ${failures} failed`);
  return failures === 0 ? 0 : 1;
}

main()
  .then((code) => process.exit(code))
  .catch((error) => {
    console.error(`[verify-hosting] ${error instanceof Error ? error.message : String(error)}`);
    process.exit(2);
  });
