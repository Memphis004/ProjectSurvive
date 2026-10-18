// Measure how long a built player takes to reach the Title screen.
//
// The template's "load time" is not the navigation's `load` event: the page is a canvas
// and the game is the thing that matters, so the number worth quoting is the moment the
// Title screen has published its report (`__projectSurviveTitle`) after a cold page load.
// That is the same handover `title.spec.ts` asserts on, measured instead of asserted.
//
//   node tests/web/scripts/measure-load-to-title.mjs [url] [runs] [out.json]
//
// Each run is a fresh page in the same browser: the WASM is re-fetched and re-compiled
// per page, which is what a first visitor pays. `domcontentloadedMs` is reported beside
// `titleMs` so a slow boot can be attributed to the network half or the player half.
//
// The browser is the one tools/versions.lock.json records (installed Chrome through
// Playwright), with the same launch arguments the specs use: a timing from swiftshader
// would not describe the desktop target.

import { chromium } from "@playwright/test";
import { writeFile } from "node:fs/promises";

import { PLAYER_BOOT_TIMEOUT_MS, TITLE } from "../globals.mjs";

const url = process.argv[2] ?? process.env.BASE_URL ?? "http://127.0.0.1:8080/";
const runs = Number.parseInt(process.argv[3] ?? "3", 10);
const out = process.argv[4] ?? "artifacts/t5/load-to-title.json";

const browser = await chromium.launch({
  channel: "chrome",
  args: ["--use-angle=default", "--enable-unsafe-swiftshader"],
});

const samples = [];
for (let run = 1; run <= runs; run += 1) {
  const page = await browser.newPage();
  const errors = [];
  page.on("pageerror", (error) => errors.push(error.message));

  const started = Date.now();
  await page.goto(url, { waitUntil: "domcontentloaded" });
  const domContentLoadedMs = Date.now() - started;

  let titleMs = null;
  try {
    await page.waitForFunction((name) => Boolean(window[name]), TITLE, {
      timeout: PLAYER_BOOT_TIMEOUT_MS,
      polling: 100,
    });
    titleMs = Date.now() - started;
  } catch {
    // A run that never reaches Title is recorded as such, not retried away.
  }

  const title = await page.evaluate((name) => window[name] ?? null, TITLE);
  const scene = await page.evaluate(() => window.__projectSurviveScene ?? null);
  samples.push({ run, domContentLoadedMs, titleMs, scene, title, errors });
  console.log(
    `run ${run}: domcontentloaded ${domContentLoadedMs} ms, title ${titleMs === null ? "never" : `${titleMs} ms`}` +
      (errors.length > 0 ? `, ${errors.length} page error(s)` : ""),
  );
  await page.close();
}

await browser.close();

const reached = samples.map((sample) => sample.titleMs).filter((ms) => ms !== null).sort((a, b) => a - b);
const median = reached.length === 0 ? null : reached[Math.floor((reached.length - 1) / 2)];

const report = {
  measuredFrom: url,
  at: new Date().toISOString(),
  browser: "Google Chrome (Playwright channel: chrome)",
  runs: samples,
  reachedTitle: reached.length,
  medianTitleMs: median,
};
await writeFile(out, `${JSON.stringify(report, null, 2)}\n`, "utf8");
console.log(`median time to Title: ${median === null ? "never reached" : `${median} ms`} (${reached.length}/${samples.length} runs) -> ${out}`);
