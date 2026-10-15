// Save the layout the camp publishes, beside a screenshot taken at the same size.
//
// Stage 09d measures how tall Thai text actually is on screen, which needs both halves: a shot of
// the real player and the screen rectangles the player itself reported for the same frame. Reading
// the rectangles out of the page here means the crop used for the measurement is the player's own
// idea of where a shelf is, not somebody's guess from looking at the image.
//
//   node tests/web/scripts/collect-geometry.mjs [url] [width] [height] [out.json] [deviceScaleFactor]
//
// The canvas is resized, not the browser window: Unity's desktop template pins the canvas element
// at 960×600 CSS pixels, so a wide viewport alone would still render 960×600 and be upscaled.
//
// The deviceScaleFactor matters because the rectangles are in the player's own screen pixels: at 2,
// a 1280×720 CSS canvas is a 2560×1440 surface and the published rectangles are twice as large, so
// a measurement of text ink has to be taken against the shot with the same factor.

import { chromium } from "@playwright/test";
import { writeFile } from "node:fs/promises";

const url = process.argv[2] ?? process.env.BASE_URL ?? "http://127.0.0.1:8080/";
const width = Number.parseInt(process.argv[3] ?? "1920", 10);
const height = Number.parseInt(process.argv[4] ?? "1080", 10);
const out = process.argv[5] ?? "artifacts/stage09d/camp-geometry-1920x1080.json";
const deviceScaleFactor = Number.parseFloat(process.argv[6] ?? "1");

const browser = await chromium.launch({
  channel: "chrome",
  args: ["--use-angle=default", "--enable-unsafe-swiftshader"],
});
const page = await browser.newPage({ viewport: { width, height }, deviceScaleFactor });

page.on("pageerror", (error) => console.log(`[pageerror] ${error.message}`));

const boot = Date.now();
await page.goto(`${url}?scene=Camp`, { waitUntil: "domcontentloaded" });
await page.waitForFunction(() => Boolean(window.__projectSurviveCampGeometry), null, {
  timeout: 180_000,
  polling: 500,
});

await page.evaluate(
  (size) => {
    const canvas = document.querySelector("canvas");
    canvas.style.width = `${size.width}px`;
    canvas.style.height = `${size.height}px`;
    window.dispatchEvent(new Event("resize"));
  },
  { width, height },
);

// The player adopts the new element size and re-maps pointer coordinates a moment later.
await page.waitForTimeout(1000);

const geometry = await page.evaluate(() => window.__projectSurviveCampGeometry);
const state = await page.evaluate(() => window.__projectSurviveCamp);

await writeFile(
  out,
  `${JSON.stringify({ collectedFrom: url, canvas: { width, height }, deviceScaleFactor, at: new Date().toISOString(), geometry, state }, null, 2)}\n`,
  "utf8",
);

console.log(`booted in ${((Date.now() - boot) / 1000).toFixed(1)}s`);
console.log(
  `canvas ${geometry.canvasWidth}x${geometry.canvasHeight} device pixels at deviceScaleFactor ${deviceScaleFactor}, ` +
    `${geometry.rects.length} regions, ${geometry.shelves.length} shelves`,
);
console.log(`wrote ${out}`);

await browser.close();
