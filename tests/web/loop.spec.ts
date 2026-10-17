import { expect, test, type Page } from "@playwright/test";

import {
  ENCOUNTER,
  HANDOVER_TIMEOUT_MS,
  HUB,
  PLAYER_BOOT_TIMEOUT_MS,
  RESULT,
  SCENE,
  SMOKE,
  TITLE,
} from "./globals.mjs";
import type { Control, EncounterReport, HubReport, ResultReport, ScreenReport, TitleReport } from "./report-types";

/**
 * The template's loop, walked in a built player: **Title → Hub → Encounter → Hub → Result → Title**.
 *
 * This is the browser-side twin of the PlayMode test that loops Hub ⇄ Encounter twenty times and
 * checks that the run scope survives it. What a built player can add is that the loop is reachable
 * with a pointer: every step here is a click at the point the screen published, and every assertion
 * is the game's own answer — the scene marker changing, and the next screen's report.
 *
 * Documents this spec reads, all on `window`:
 *
 *   __projectSurviveSmoke      - the boot self-check
 *   __projectSurviveScene      - the scene on screen
 *   __projectSurviveTitle      - the title screen, and where its start control is
 *   __projectSurviveHub        - the Hub, its counters, and where its two controls are
 *   __projectSurviveEncounter  - the Encounter, its progress line, and its two controls
 *   __projectSurviveResult     - the summary, and where its two controls are
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

function readGlobal<T>(page: Page, name: string): Promise<T> {
  return page.evaluate((globalName) => (window as unknown as Record<string, unknown>)[globalName] as T, name);
}

async function waitForGlobal(page: Page, name: string): Promise<void> {
  await page.waitForFunction(
    (globalName) => Boolean((window as unknown as Record<string, unknown>)[globalName]),
    name,
    { timeout: PLAYER_BOOT_TIMEOUT_MS, polling: 500 },
  );
}

async function waitForScene(page: Page, scene: string): Promise<void> {
  await expect
    .poll(() => readGlobal<string>(page, SCENE), { timeout: HANDOVER_TIMEOUT_MS, polling: 250 })
    .toBe(scene);
}

/**
 * Presses the control named `controlName` on the screen whose report is `reportGlobal`, at the point
 * that screen published, and returns the report it was read from.
 *
 * A synthesised click into a WebGL player is occasionally swallowed (measured in Stage 09: two runs of
 * four), so a press is retried the way a player would press again. The retry is a nudge and a second
 * click, never a second call into the game's API — the point of the spec is the pointer path.
 */
async function pressControl(
  page: Page,
  reportGlobal: string,
  controlName: string,
  expectedScene: string,
): Promise<ScreenReport> {
  const report = await readGlobal<ScreenReport>(page, reportGlobal);
  const control = report.buttons.find((candidate: Control) => candidate.name === controlName);
  expect(control, `the screen published no '${controlName}' control: ${JSON.stringify(report.buttons)}`).toBeDefined();

  const box = await page.locator("canvas").boundingBox();
  expect(box, "the player has no canvas element").not.toBeNull();

  const point = {
    x: box!.x + (control!.x / report.canvasWidth) * box!.width,
    y: box!.y + ((report.canvasHeight - control!.y) / report.canvasHeight) * box!.height,
  };

  let arrived = false;
  for (let attempt = 1; attempt <= 4 && !arrived; attempt += 1) {
    await page.mouse.move(point.x, point.y);
    await page.waitForTimeout(200);
    await page.mouse.down();
    await page.waitForTimeout(200);
    await page.mouse.up();

    arrived = await expect
      .poll(() => readGlobal<string>(page, SCENE), { timeout: 10_000, polling: 250 })
      .toBe(expectedScene)
      .then(
        () => true,
        () => false,
      );

    if (!arrived) {
      console.log(`the '${controlName}' click was not registered (attempt ${attempt} of 4)`);
      await page.mouse.move(point.x, point.y - 60);
      await page.waitForTimeout(800);
    }
  }

  expect(arrived, `pressing '${controlName}' never reached ${expectedScene}`).toBe(true);
  return report;
}

test.describe("the template's loop in the browser", () => {
  test("walks Title → Hub → Encounter → Hub → Result → Title by pressing the controls", async ({ page }) => {
    test.setTimeout(420_000);
    const pageErrors = collectPageErrors(page);

    await page.goto("/", { waitUntil: "domcontentloaded" });
    await waitForGlobal(page, SMOKE);
    await waitForGlobal(page, SCENE);
    await waitForScene(page, "Title");

    // Leg 1 — the title starts a run.
    await waitForGlobal(page, TITLE);
    const title = await readGlobal<TitleReport>(page, TITLE);
    expect(title.startLabel).toMatch(/[\u0E00-\u0E7F]/);
    await pressControl(page, TITLE, "start", "Hub");

    // Leg 2 — the Hub shows a run that has cleared nothing, and departs.
    await waitForGlobal(page, HUB);
    const firstHub = await readGlobal<HubReport>(page, HUB);
    console.log("hub report (start of run):", JSON.stringify(firstHub));
    expect(firstHub.encountersLine).toContain("0");
    expect(firstHub.departLabel).toMatch(/[\u0E00-\u0E7F]/);
    expect(firstHub.buttons.map((button: Control) => button.name).sort()).toEqual(["depart", "finish"]);
    await page.screenshot({ path: "test-results/web-loop-hub-0.png", fullPage: false });

    await pressControl(page, HUB, "depart", "Encounter");

    // Leg 3 — the encounter reports its progress and is cleared.
    await waitForGlobal(page, ENCOUNTER);
    const encounter = await readGlobal<EncounterReport>(page, ENCOUNTER);
    console.log("encounter report:", JSON.stringify(encounter));
    expect(encounter.progressLine).toContain("1");
    expect(encounter.winLabel).toMatch(/[\u0E00-\u0E7F]/);
    expect(encounter.loseLabel).toMatch(/[\u0E00-\u0E7F]/);
    expect(encounter.buttons.map((button: Control) => button.name).sort()).toEqual(["lose", "win"]);
    await page.screenshot({ path: "test-results/web-loop-encounter.png", fullPage: false });

    await pressControl(page, ENCOUNTER, "win", "Hub");

    // Leg 4 — the Hub counts the cleared encounter, and the run is ended from here.
    await waitForGlobal(page, HUB);
    const secondHub = await readGlobal<HubReport>(page, HUB);
    console.log("hub report (after one encounter):", JSON.stringify(secondHub));
    expect(secondHub.encountersLine).toContain("1");
    await page.screenshot({ path: "test-results/web-loop-hub-1.png", fullPage: false });

    await pressControl(page, HUB, "finish", "Result");

    // Leg 5 — the summary reports the run it is summarising, then hands back to the title.
    await waitForGlobal(page, RESULT);
    const result = await readGlobal<ResultReport>(page, RESULT);
    console.log("result report:", JSON.stringify(result));
    expect(result.finished).toBe(true);
    expect(result.encounters).toBe(1);
    expect(result.headline).toMatch(/[\u0E00-\u0E7F]/);
    expect(result.buttons.map((button: Control) => button.name).sort()).toEqual(["restart", "title"]);
    await page.screenshot({ path: "test-results/web-loop-result.png", fullPage: false });

    await pressControl(page, RESULT, "title", "Title");
    await waitForGlobal(page, TITLE);

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });
});
