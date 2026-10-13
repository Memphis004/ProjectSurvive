import { defineConfig } from "@playwright/test";

/**
 * Playwright configuration for the ProjectSurvive WebGL build.
 *
 * The build is NOT started here: Unity's WebGL player needs the build produced by
 * `tools/build-web.ps1` and served by `tools/serve-web.mjs`, and hiding that behind
 * a Playwright `webServer` block would make a failing build look like a failing
 * front end. `BASE_URL` lets the harness decide where the player is.
 *
 * The Chrome channel is used deliberately: tools/versions.lock.json records the
 * reference browser, and ADR 0003 says performance and compatibility claims are only
 * valid for a named browser and version. Playwright's bundled Chromium would be a
 * third browser nobody measured.
 */
export default defineConfig({
  testDir: ".",
  testMatch: /.*\.spec\.ts$/,
  timeout: 180_000,
  expect: { timeout: 150_000 },
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: [
    ["list"],
    ["html", { outputFolder: "playwright-report", open: "never" }],
  ],
  use: {
    baseURL: process.env.BASE_URL ?? "http://127.0.0.1:8080",
    channel: "chrome",
    trace: "retain-on-failure",
    screenshot: "on",
    launchOptions: {
      // A WebGL2 player in a headless browser needs a real GPU path; this keeps the
      // run faithful to the desktop target instead of testing swiftshader.
      args: ["--use-angle=default", "--enable-unsafe-swiftshader"],
    },
  },
  outputDir: "test-results",
});
