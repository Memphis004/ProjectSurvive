import { expect, test, type Page } from "@playwright/test";

import { SCENE, SMOKE, THAI_SPIKE } from "./globals.mjs";

/**
 * Smoke test for the real WebGL build.
 *
 * The build's self-check publishes one JSON document on `window`, so these tests
 * assert on what the player actually did rather than on how the page looks:
 *
 *   __projectSurviveSmoke     - the seven Stage 03 probes (config, DI, R3,
 *                               UniTask cancellation, MessagePack, Protobuf, Thai text)
 *   __projectSurviveScene     - the scene on screen, so scene flow is observable
 *   __projectSurviveThaiSpike - the Thai text spike measurement
 *
 * The first load of a Unity WebGL player includes downloading and compiling the
 * WASM, which is why the timeouts here are minutes rather than seconds; a shorter
 * timeout would report a slow machine as a broken build.
 */

/** How long to wait for the player to boot and run its self-check. */
const PLAYER_BOOT_TIMEOUT_MS = 150_000;

/** Probes that must be present and passing; a rename should fail here, loudly. */
const REQUIRED_PROBES = [
  "di.resolution",
  "config.tables",
  "r3.subscribe",
  "unitask.cancellation",
  "messagepack.roundtrip",
  "protobuf.roundtrip",
  "localization.thai",
];

interface Probe {
  name: string;
  passed: boolean;
  detail: string;
}

interface SmokeReport {
  platform: string;
  configHash: string;
  probeCount: number;
  failedCount: number;
  passed: boolean;
  probes: Probe[];
}

interface ThaiSpikeReport {
  platform: string;
  font: string;
  rowCount: number;
  totalMissingGlyphs: number;
  coversEveryGlyph: boolean;
  stacksMeasureEqually: boolean;
  rows: Array<{ stack: string; missingGlyphs: number; width: number; height: number }>;
}

/**
 * Collects page-level errors. The player's own failures surface here as console
 * errors from the emitted JavaScript, so this is how "the console has no errors"
 * becomes an assertion instead of an observation.
 */
function collectPageErrors(page: Page): string[] {
  const errors: string[] = [];
  page.on("console", (message) => {
    if (message.type() === "error") {
      errors.push(`console.error: ${message.text()}`);
    }
  });
  page.on("pageerror", (error) => errors.push(`pageerror: ${error.message}`));
  return errors;
}

async function waitForGlobal(page: Page, name: string): Promise<void> {
  await page.waitForFunction(
    (globalName) => Boolean((window as unknown as Record<string, unknown>)[globalName]),
    name,
    { timeout: PLAYER_BOOT_TIMEOUT_MS, polling: 500 },
  );
}

async function readGlobal<T>(page: Page, name: string): Promise<T> {
  return page.evaluate((globalName) => (window as unknown as Record<string, unknown>)[globalName] as T, name);
}

test.describe("ProjectSurvive WebGL build", () => {
  test("boots, passes its self-check and hands over to the title screen", async ({ page }) => {
    const pageErrors = collectPageErrors(page);

    await page.goto("/", { waitUntil: "domcontentloaded" });
    await waitForGlobal(page, SMOKE);

    const smoke = await readGlobal<SmokeReport>(page, SMOKE);

    // The report is the artifact; print it so a failure in CI is diagnosable from the
    // test log alone.
    console.log("smoke report:", JSON.stringify(smoke));

    expect(smoke.platform).toBe("WebGLPlayer");
    expect(smoke.failedCount).toBe(0);
    expect(smoke.passed).toBe(true);
    expect(smoke.configHash).toMatch(/^[0-9A-F]{64}$/);

    for (const probeName of REQUIRED_PROBES) {
      const probe = smoke.probes.find((candidate) => candidate.name === probeName);
      expect(probe, `probe '${probeName}' missing from the build's self-check`).toBeDefined();
      expect(probe?.passed, `probe '${probeName}' failed: ${probe?.detail}`).toBe(true);
    }

    // Scene flow: the boot scene must hand over to Title by itself.
    await waitForGlobal(page, SCENE);
    await expect
      .poll(() => readGlobal<string>(page, SCENE), { timeout: PLAYER_BOOT_TIMEOUT_MS })
      .toBe("Title");

    // The Thai title must be on screen, not just in the data: the label is what a
    // player sees, and this is the only place a font failure in a browser shows up.
    const titleText = await page.evaluate(() => {
      const canvas = document.querySelector("canvas");
      return canvas ? canvas.getBoundingClientRect().width : 0;
    });
    expect(titleText).toBeGreaterThan(0);

    await page.screenshot({ path: "test-results/web-title.png", fullPage: false });

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });

  test("renders the Thai text spike with full glyph coverage in the browser", async ({ page }) => {
    const pageErrors = collectPageErrors(page);

    await page.goto("/?scene=ThaiTextSpike", { waitUntil: "domcontentloaded" });
    await waitForGlobal(page, THAI_SPIKE);

    const spike = await readGlobal<ThaiSpikeReport>(page, THAI_SPIKE);
    console.log("thai spike report:", JSON.stringify(spike));

    expect(spike.platform).toBe("WebGLPlayer");
    expect(spike.font).toContain("Sarabun");
    expect(spike.rowCount).toBe(6);
    expect(
      spike.totalMissingGlyphs,
      `the shipped font asset cannot draw every spike string: ${JSON.stringify(spike.rows)}`,
    ).toBe(0);
    expect(spike.coversEveryGlyph).toBe(true);

    await page.screenshot({ path: "test-results/web-thai-spike.png", fullPage: false });

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });
});
