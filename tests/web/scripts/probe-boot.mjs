// Throwaway diagnostic (Stage 03): print everything the WebGL player says.
//
// The Playwright suite asserts on the published report; when the report itself says
// "the container was never built", the cause is an exception inside the player, and
// the only place that is visible is the browser console. This script relays it.
//
//   node tests/web/scripts/probe-boot.mjs [url]

import { chromium } from "@playwright/test";

import { SCENE, SMOKE, THAI_SPIKE } from "../globals.mjs";

const url = process.argv[2] ?? process.env.BASE_URL ?? "http://127.0.0.1:8080/";

const browser = await chromium.launch({
  channel: "chrome",
  args: ["--use-angle=default", "--enable-unsafe-swiftshader"],
});
const page = await browser.newPage();

page.on("console", (message) => console.log(`[console:${message.type()}] ${message.text()}`));
page.on("pageerror", (error) => console.log(`[pageerror] ${error.message}\n${error.stack ?? ""}`));
page.on("requestfailed", (request) =>
  console.log(`[requestfailed] ${request.url()} ${request.failure()?.errorText ?? ""}`),
);

console.log(`opening ${url}`);
await page.goto(url, { waitUntil: "domcontentloaded" });

try {
  await page.waitForFunction((name) => Boolean(window[name]), SMOKE, {
    timeout: 150_000,
    polling: 500,
  });
} catch (error) {
  console.log(`[wait] smoke global never appeared: ${error.message}`);
}

console.log("smoke:", JSON.stringify(await page.evaluate((name) => window[name] ?? null, SMOKE)));
console.log("scene:", JSON.stringify(await page.evaluate((name) => window[name] ?? null, SCENE)));
console.log("thai :", JSON.stringify(await page.evaluate((name) => window[name] ?? null, THAI_SPIKE)));

await browser.close();
