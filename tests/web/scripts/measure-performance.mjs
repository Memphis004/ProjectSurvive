// Stage 15: the browser performance baseline for the release candidate.
//
// Every number the performance report quotes comes from here. It runs a REAL
// browser against a REAL build over HTTP and records the machine, browser version
// and build identity beside the numbers, because a performance claim without them
// is an anecdote (ADR 0003).
//
//   node tests/web/scripts/measure-performance.mjs [--url http://127.0.0.1:8092] [--headless]
//
// What is measured, per scenario:
//   * load time - navigation start to the boot self-check, and to the title screen
//     publishing itself, cold (new browser context) and warm (reload, cache live);
//   * frame time - the interval between the game's own requestAnimationFrame
//     callbacks, reported as p50 / p95 / p99 / max. p95 is the GDD's budget number;
//   * memory - JS heap from `performance.memory` and the WebAssembly heap from the
//     `WebAssembly.Memory` object the player instantiates (captured by patching the
//     constructor before any page script runs). They are reported separately
//     because the GDD's ~350 MB is about the Unity/Wasm heap, not about the DOM;
//   * allocation - the growth of both heaps over a measured window, which is the
//     only allocation signal a browser exposes without instrumenting the player;
//   * entity count - read from the battle's own published report (`entities`), so a
//     frame time is always attached to the load it was measured under.
//
// The canvas is resized to 1920x1080 CSS pixels (the GDD's test resolution) the
// way the layout tools do it: Unity's desktop template pins the element at 960x600
// and a wide viewport alone would still render 960x600 and upscale it.
//
// Chrome is launched HEADFUL by default. Frame timing from a headless compositor
// is not the thing the player ships into, and ADR 0003 says only a real browser
// run counts; `--headless` exists for a machine with no display, and the report
// records which mode produced a number.
//
// Writes artifacts/perf/performance.json and artifacts/perf/performance.md.

import { chromium } from "@playwright/test";
import { execSync } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import os from "node:os";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { dirname } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..", "..", "..");
const artifactDirectory = join(repoRoot, "artifacts", "perf");

function argument(name, fallback) {
  const index = process.argv.indexOf(name);
  return index >= 0 && process.argv[index + 1] ? process.argv[index + 1] : fallback;
}

const baseUrl = argument("--url", process.env.BASE_URL ?? "http://127.0.0.1:8092");
const headless = process.argv.includes("--headless");
const idleSeconds = Number.parseInt(argument("--idle-seconds", "10"), 10);
const battleSeconds = Number.parseInt(argument("--battle-seconds", "45"), 10);

const canvasSize = { width: 1920, height: 1080 };

function readJson(path) {
  return existsSync(path) ? JSON.parse(readFileSync(path, "utf8")) : null;
}

function gitHead() {
  try {
    return execSync("git rev-parse HEAD", { cwd: repoRoot, encoding: "utf8" }).trim();
  } catch {
    return null;
  }
}

function percentile(sorted, fraction) {
  if (sorted.length === 0) {
    return null;
  }
  const position = (sorted.length - 1) * fraction;
  const lower = Math.floor(position);
  const upper = Math.ceil(position);
  if (lower === upper) {
    return sorted[lower];
  }
  return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
}

function summarise(samples) {
  if (samples.length === 0) {
    return { count: 0 };
  }
  const sorted = [...samples].sort((left, right) => left - right);
  const mean = sorted.reduce((total, value) => total + value, 0) / sorted.length;
  return {
    count: sorted.length,
    meanMs: Number(mean.toFixed(2)),
    p50Ms: Number(percentile(sorted, 0.5).toFixed(2)),
    p95Ms: Number(percentile(sorted, 0.95).toFixed(2)),
    p99Ms: Number(percentile(sorted, 0.99).toFixed(2)),
    maxMs: Number(sorted[sorted.length - 1].toFixed(2)),
    over20ms: sorted.filter((value) => value > 20).length,
    over33ms: sorted.filter((value) => value > 33).length,
  };
}

// Installed before any page script: the player's Wasm heap is the `memory` export of
// the module it instantiates, and it grows during play. Capturing the instance is how
// the heap is read without publishing anything from the player itself (a Release
// player has no harness, by design). Emscripten in this build creates its memory in
// the wasm module rather than with `new WebAssembly.Memory`, so both are hooked: the
// instance export is the one that exists here, the constructor hook is the fallback
// for a build that imports its memory instead.
const captureWasmMemory = () => {
  window.__psWasmMemories = [];

  const remember = (memory) => {
    try {
      if (memory && memory.buffer && !window.__psWasmMemories.includes(memory)) {
        window.__psWasmMemories.push(memory);
      }
    } catch {
      // A detached buffer is not a heap; nothing to record.
    }
  };

  for (const name of ["instantiate", "instantiateStreaming"]) {
    const original = WebAssembly[name];
    if (typeof original !== "function") {
      continue;
    }
    WebAssembly[name] = async function patchedInstantiate(...args) {
      const result = await original.apply(this, args);
      remember((result?.instance ?? result)?.exports?.memory);
      return result;
    };
  }

  const OriginalMemory = WebAssembly.Memory;
  const patched = function PatchedMemory(descriptor) {
    const memory = new OriginalMemory(descriptor);
    remember(memory);
    return memory;
  };
  patched.prototype = OriginalMemory.prototype;
  WebAssembly.Memory = patched;

  window.__psLongTasks = [];
  try {
    new PerformanceObserver((list) => {
      for (const entry of list.getEntries()) {
        window.__psLongTasks.push({ startTime: entry.startTime, duration: entry.duration });
      }
    }).observe({ type: "longtask", buffered: true });
  } catch {
    // `longtask` is Chromium-only; the field stays empty elsewhere and the report
    // says so by omission rather than by inventing a number.
  }
};

async function memorySnapshot(page) {
  return page.evaluate(() => {
    const memories = (window.__psWasmMemories ?? [])
      .map((memory) => {
        try {
          return memory.buffer.byteLength;
        } catch {
          return 0;
        }
      })
      .filter((bytes) => bytes > 0);
    const heap = window.performance?.memory;
    return {
      jsHeapUsedBytes: heap?.usedJSHeapSize ?? null,
      jsHeapTotalBytes: heap?.totalJSHeapSize ?? null,
      wasmHeapsBytes: memories,
      wasmHeapBytes: memories.length > 0 ? Math.max(...memories) : null,
      devicePixelRatio: window.devicePixelRatio,
      canvas: (() => {
        const canvas = document.querySelector("canvas");
        if (!canvas) {
          return null;
        }
        return {
          cssWidth: canvas.clientWidth,
          cssHeight: canvas.clientHeight,
          backingWidth: canvas.width,
          backingHeight: canvas.height,
        };
      })(),
    };
  });
}

async function frameSamples(page, seconds) {
  return page.evaluate(
    (durationSeconds) =>
      new Promise((resolvePromise) => {
        const deltas = [];
        let last = null;
        const deadline = performance.now() + durationSeconds * 1000;

        const step = (timestamp) => {
          if (last !== null) {
            deltas.push(timestamp - last);
          }
          last = timestamp;

          if (timestamp >= deadline) {
            resolvePromise(deltas);
            return;
          }
          requestAnimationFrame(step);
        };

        requestAnimationFrame(step);
      }),
    seconds,
  );
}

async function resizeCanvas(page) {
  await page.evaluate(
    (size) => {
      const canvas = document.querySelector("canvas");
      if (!canvas) {
        return;
      }
      canvas.style.width = `${size.width}px`;
      canvas.style.height = `${size.height}px`;
      window.dispatchEvent(new Event("resize"));
    },
    canvasSize,
  );
}

async function waitForScene(page, scene, timeoutMs = 240_000) {
  await page.waitForFunction(
    (expected) => (window.__projectSurviveScene ?? null) === expected,
    scene,
    { timeout: timeoutMs, polling: 250 },
  );
}

async function waitForBoot(page, timeoutMs = 240_000) {
  await page.waitForFunction(
    () => {
      const smoke = window.__projectSurviveSmoke;
      return Boolean(smoke && smoke.passed === true);
    },
    null,
    { timeout: timeoutMs, polling: 200 },
  );
}

function summariseReport(report) {
  if (!report) {
    return null;
  }
  return {
    phase: report.phase,
    stepsRun: Number(report.stepsRun),
    droppedTicks: Number(report.droppedTicks),
    entities: report.entities ?? null,
    viewsBound: report.viewsBound ?? null,
    poolCapacity: report.poolCapacity ?? null,
    viewsGrownDuringPlay: report.viewsGrownDuringPlay ?? null,
    droppedEvents: report.droppedEvents ?? null,
  };
}

async function collectScenario(page, options) {
  const started = await memorySnapshot(page);
  const frames = await frameSamples(page, options.seconds);

  // The player's own report, read after the frame window: it carries the entity
  // count and the ticker's dropped ticks for the load the frames belong to.
  const report = await page.evaluate((globalName) => window[globalName] ?? null, options.reportGlobal);

  const ended = await memorySnapshot(page);
  const longTasks = await page.evaluate(() => window.__psLongTasks ?? []);

  return {
    name: options.name,
    seconds: options.seconds,
    frameTime: summarise(frames),
    fps: frames.length === 0 ? null : Number((frames.length / options.seconds).toFixed(1)),
    memory: {
      before: started,
      after: ended,
      jsHeapGrowthBytes:
        started.jsHeapUsedBytes !== null && ended.jsHeapUsedBytes !== null
          ? ended.jsHeapUsedBytes - started.jsHeapUsedBytes
          : null,
      wasmHeapGrowthBytes:
        started.wasmHeapBytes !== null && ended.wasmHeapBytes !== null
          ? ended.wasmHeapBytes - started.wasmHeapBytes
          : null,
    },
    longTasks,
    report: summariseReport(report),
  };
}

async function loadScenario(page, url, scene, label) {
  const startedAt = Date.now();
  await page.goto(url, { waitUntil: "domcontentloaded" });
  await waitForBoot(page);

  const bootAt = await page.evaluate(() => performance.now());
  await waitForScene(page, scene);
  const titleAt = await page.evaluate(() => performance.now());

  const resources = await page.evaluate(() =>
    performance
      .getEntriesByType("resource")
      .filter((entry) => entry.name.includes("/Build/") || entry.name.endsWith(".html"))
      .map((entry) => ({
        name: entry.name.split("/").pop(),
        transferSize: entry.transferSize,
        encodedBodySize: entry.encodedBodySize,
        decodedBodySize: entry.decodedBodySize,
        durationMs: Number(entry.duration.toFixed(1)),
      })),
  );

  return {
    label,
    wallClockMs: Date.now() - startedAt,
    bootSelfCheckMs: Number(bootAt.toFixed(1)),
    sceneReadyMs: Number(titleAt.toFixed(1)),
    resources,
  };
}

async function main() {
  mkdirSync(artifactDirectory, { recursive: true });

  const buildInfo = readJson(join(repoRoot, "UnityProject", "Builds", "Web", "build-info.json"));
  const buildReport = readJson(join(repoRoot, "UnityProject", "Builds", "Web", "build-report.json"));
  const compression = readJson(join(repoRoot, "UnityProject", "Builds", "Web", "compression-report.json"));

  const browser = await chromium.launch({
    channel: "chrome",
    headless,
    args: [
      "--use-angle=default",
      "--enable-unsafe-swiftshader",
      "--disable-background-timer-throttling",
      "--disable-renderer-backgrounding",
      "--window-size=1920,1080",
    ],
  });

  const result = {
    at: new Date().toISOString(),
    build: {
      gitHead: gitHead(),
      buildInfo,
      sizeSettings: buildReport?.sizeSettings ?? null,
      sizeProfile: buildReport?.sizeProfile ?? null,
      initialPayload: compression?.initialPayload ?? null,
    },
    machine: {
      cpu: os.cpus()[0]?.model ?? null,
      logicalProcessors: os.cpus().length,
      totalMemoryBytes: os.totalmem(),
      platform: `${os.platform()} ${os.release()}`,
      node: process.version,
    },
    browser: {
      name: "Google Chrome (Playwright channel)",
      version: browser.version(),
      headless,
      viewport: canvasSize,
    },
    url: baseUrl,
    scenarios: {},
    loads: {},
  };

  let context = await browser.newContext({ viewport: canvasSize, deviceScaleFactor: 1 });
  await context.addInitScript(captureWasmMemory);

  try {
    // Cold load: a fresh context, so nothing is in the HTTP cache.
    let page = await context.newPage();
    const pageErrors = [];
    page.on("pageerror", (error) => pageErrors.push(error.message));
    result.loads.cold = await loadScenario(page, `${baseUrl}/`, "Title", "cold boot to title");
    result.loads.cold.pageErrors = pageErrors;

    await resizeCanvas(page);
    await page.waitForTimeout(1500);
    result.scenarios.titleIdle = await collectScenario(page, {
      name: "title idle",
      seconds: idleSeconds,
      reportGlobal: "__projectSurviveTitle",
    });

    // Warm load: same context and cache, a full navigation to the title.
    result.loads.warm = await loadScenario(page, `${baseUrl}/`, "Title", "warm reload to title");

    // Camp: a normal (non-combat) screen with the world drawn.
    await page.close();
    page = await context.newPage();
    page.on("pageerror", (error) => pageErrors.push(error.message));
    await page.goto(`${baseUrl}/?scene=Camp`, { waitUntil: "domcontentloaded" });
    await page.waitForFunction(() => Boolean(window.__projectSurviveCamp), null, {
      timeout: 240_000,
      polling: 250,
    });
    await resizeCanvas(page);
    await page.waitForTimeout(1500);
    result.scenarios.campIdle = await collectScenario(page, {
      name: "camp (sala)",
      seconds: idleSeconds,
      reportGlobal: "__projectSurviveCamp",
    });

    // Battle floor 1: the normal combat load.
    await page.close();
    page = await context.newPage();
    page.on("pageerror", (error) => pageErrors.push(error.message));
    await page.goto(`${baseUrl}/?scene=Battle`, { waitUntil: "domcontentloaded" });
    await page.waitForFunction(
      () => Number(window.__projectSurviveBattle?.stepsRun ?? 0) > 30,
      null,
      { timeout: 240_000, polling: 250 },
    );
    await resizeCanvas(page);
    await page.waitForTimeout(1000);

    // Sample the report once a second *during* the frame window: the battle report
    // carries the entity count and the ticker's dropped ticks, which is what makes
    // the frame time attributable to a load.
    const samples = [];
    const sampling = (async () => {
      const until = Date.now() + battleSeconds * 1000;
      while (Date.now() < until) {
        samples.push(await page.evaluate(() => window.__projectSurviveBattle ?? null));
        await page.waitForTimeout(1000);
      }
    })();

    const battle = await collectScenario(page, {
      name: "battle floor 1",
      seconds: battleSeconds,
      reportGlobal: "__projectSurviveBattle",
    });
    await sampling;

    const valid = samples.filter(Boolean);
    battle.reportSamples = {
      count: valid.length,
      peakEntities: valid.reduce((peak, sample) => Math.max(peak, Number(sample.entities ?? 0)), 0),
      last: summariseReport(valid[valid.length - 1] ?? null),
      droppedTicks: valid.reduce((total, sample) => Math.max(total, Number(sample.droppedTicks ?? 0)), 0),
    };
    battle.pageErrors = pageErrors;
    result.scenarios.battle = battle;
  } finally {
    await context.close();
    await browser.close();
  }

  const budget = {
    frameTimeP95Ms: 20,
    memoryBytes: 350 * 1024 * 1024,
    initialDownloadGzipBytes: 30 * 1024 * 1024,
    initialDownloadBrotliBytes: 15 * 1024 * 1024,
    allocation: "near zero after warm-up",
  };

  result.budget = budget;
  result.verdict = {
    battleFrameP95: result.scenarios.battle?.frameTime?.p95Ms ?? null,
    battleFrameP95Passes: (result.scenarios.battle?.frameTime?.p95Ms ?? Infinity) <= budget.frameTimeP95Ms,
    titleFrameP95: result.scenarios.titleIdle?.frameTime?.p95Ms ?? null,
    campFrameP95: result.scenarios.campIdle?.frameTime?.p95Ms ?? null,
    wasmHeapBytes: result.scenarios.battle?.memory?.after?.wasmHeapBytes ?? null,
    // `null` is NOT MEASURED, never PASS and never FAIL: a missing heap reading is a gap
    // in the instrument, not a verdict on the player.
    wasmHeapPasses:
      result.scenarios.battle?.memory?.after?.wasmHeapBytes === null ||
      result.scenarios.battle?.memory?.after?.wasmHeapBytes === undefined
        ? null
        : result.scenarios.battle.memory.after.wasmHeapBytes <= budget.memoryBytes,
    payloadGzipBytes: compression?.initialPayload?.gzip ?? null,
    payloadBrotliBytes: compression?.initialPayload?.brotli ?? null,
    payloadGzipPasses: (compression?.initialPayload?.gzip ?? Infinity) < budget.initialDownloadGzipBytes,
    payloadBrotliPasses: (compression?.initialPayload?.brotli ?? Infinity) < budget.initialDownloadBrotliBytes,
  };

  writeFileSync(join(artifactDirectory, "performance.json"), `${JSON.stringify(result, null, 2)}\n`);

  const ms = (value) => (value === null || value === undefined ? "n/a" : `${value.toFixed(2)} ms`);
  const size = (value) => (value === null || value === undefined ? "n/a" : `${(value / (1024 * 1024)).toFixed(2)} MiB`);
  const scenarioRows = Object.entries(result.scenarios)
    .map(([key, scenario]) => {
      const f = scenario.frameTime;
      return `| ${scenario.name} | ${f.count} | ${ms(f.p50Ms)} | ${ms(f.p95Ms)} | ${ms(f.p99Ms)} | ${ms(f.maxMs)} | ${f.over20ms} | ${scenario.reportSamples?.peakEntities ?? scenario.report?.entities ?? "n/a"} |`;
    })
    .join("\n");

  const markdown = `# Browser performance measurement (Stage 15)

Generated by \`node tests/web/scripts/measure-performance.mjs\` against
\`${baseUrl}\` at ${result.at}.

| | |
| --- | --- |
| Build | git \`${result.build.gitHead}\`, config hash \`${buildInfo?.configHash ?? "n/a"}\`, ${buildReport?.sizeProfile ?? "no profile"} |
| Configuration | ${buildInfo?.development === false ? "Release" : "Development"}; stripping ${buildReport?.sizeSettings?.managedStrippingLevel ?? "?"}, codegen ${buildReport?.sizeSettings?.il2cppCodeGeneration ?? "?"} |
| Machine | ${result.machine.cpu}, ${result.machine.logicalProcessors} logical processors, ${(result.machine.totalMemoryBytes / 1024 ** 3).toFixed(1)} GiB RAM, ${result.machine.platform} |
| Browser | ${result.browser.name} ${result.browser.version}, ${headless ? "headless" : "headful"}, ${canvasSize.width}x${canvasSize.height} |

## Frame time

| Scenario | Frames | p50 | p95 | p99 | max | frames > 20 ms | entities (peak) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
${scenarioRows}

Budget: p95 <= 20 ms on a normal floor. Battle p95 **${ms(result.verdict.battleFrameP95)}** ->
**${result.verdict.battleFrameP95Passes ? "PASS" : "FAIL"}**.

## Load

| Load | Wall clock | Boot self-check | Title ready |
| --- | ---: | ---: | ---: |
| Cold (new context) | ${result.loads.cold?.wallClockMs} ms | ${ms(result.loads.cold?.bootSelfCheckMs)} | ${ms(result.loads.cold?.sceneReadyMs)} |
| Warm (reload) | ${result.loads.warm?.wallClockMs} ms | ${ms(result.loads.warm?.bootSelfCheckMs)} | ${ms(result.loads.warm?.sceneReadyMs)} |

## Memory

| Scenario | JS heap before | JS heap after | growth | Wasm heap before | Wasm heap after |
| --- | ---: | ---: | ---: | ---: | ---: |
${Object.entries(result.scenarios)
  .map(
    ([, scenario]) =>
      `| ${scenario.name} | ${size(scenario.memory.before?.jsHeapUsedBytes)} | ${size(scenario.memory.after?.jsHeapUsedBytes)} | ${size(scenario.memory.jsHeapGrowthBytes)} | ${size(scenario.memory.before?.wasmHeapBytes)} | ${size(scenario.memory.after?.wasmHeapBytes)} |`,
  )
  .join("\n")}

Budget: approx. 350 MB for the Unity/Wasm heap -> battle measured **${size(result.verdict.wasmHeapBytes)}**
-> **${result.verdict.wasmHeapPasses === null ? "NOT MEASURED" : result.verdict.wasmHeapPasses ? "PASS" : "FAIL"}**.

## Initial payload (ADR 0011)

| Basis | Size | Gate |
| --- | ---: | --- |
| gzip -9 | ${size(result.verdict.payloadGzipBytes)} | < 30 MiB -> ${result.verdict.payloadGzipPasses ? "PASS" : "FAIL"} |
| Brotli q11 | ${size(result.verdict.payloadBrotliBytes)} | < 15 MiB -> ${result.verdict.payloadBrotliPasses ? "PASS" : "FAIL"} |
`;

  writeFileSync(join(artifactDirectory, "performance.md"), markdown);

  console.log(markdown);
  return result.verdict.battleFrameP95Passes ? 0 : 1;
}

main()
  .then((code) => process.exit(code))
  .catch((error) => {
    console.error(`[measure-performance] ${error instanceof Error ? error.stack : String(error)}`);
    process.exit(2);
  });
