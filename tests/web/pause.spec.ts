import { expect, test, type Page } from "@playwright/test";

import { PAUSE, SCENE } from "./globals.mjs";
import { expectTextFits } from "./textfit.mjs";

/**
 * Stage 13c-2b acceptance in the built player: **the battle's pause overlay**.
 *
 *   __projectSurvivePause   - the overlay's state, its labels and where its controls are
 *   __projectSurviveBattle  - the runner's own document (how far the floor has got)
 *   __projectSurviveCamp    - the camp's report after กลับศาลา
 *
 * The overlay's document is rewritten twice a second *while it is open*, which is what makes the
 * freeze observable: the battle's document is written by the runner, and a paused runner writes
 * nothing at all. So "the floor stopped" is read as "the overlay's `tick` stayed the same across
 * 1.5 s of wall clock", and "the floor runs again" as the battle document's `stepsRun` passing the
 * tick it was frozen at.
 *
 * The presses are aimed at the points the player published, never at a remembered pixel, and each one
 * is retried: a synthesised click into a WebGL canvas is occasionally swallowed (measured in Stage 09).
 */

const BATTLE = "__projectSurviveBattle";
const CAMP = "__projectSurviveCamp";

/** Boot: download + compile the player. */
const PLAYER_BOOT_TIMEOUT_MS = 180_000;

/** How long a press may take to show up in the published report. */
const COMMAND_TIMEOUT_MS = 20_000;

/** How long a scene handover (a fade plus a load) may take. */
const HANDOVER_TIMEOUT_MS = 30_000;

/** How long the frozen floor is watched for, in milliseconds. */
const FREEZE_WINDOW_MS = 1500;

interface Control {
  name: string;
  x: number;
  y: number;
}

interface PauseReport {
  scene: string;
  canvasWidth: number;
  canvasHeight: number;
  open: boolean;
  paused: boolean;
  tick: string | number;
  hp: number;
  requestedScene: string | null;
  lastError: string;
  labels: { title: string; warning: string; resume: string; leave: string; open: string };
  buttons: Control[];
}

interface BattleReport {
  scene: string;
  phase: string;
  stepsRun: string | number;
  ended: boolean;
}

interface CampReport {
  flow: string;
  floor: number;
  hp: number;
  bia: number;
  canDepart: boolean;
}

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
  return page.evaluate(
    (globalName) => (window as unknown as Record<string, unknown>)[globalName] as T,
    name,
  );
}

async function waitForGlobal(page: Page, name: string): Promise<void> {
  await page.waitForFunction(
    (globalName) => Boolean((window as unknown as Record<string, unknown>)[globalName]),
    name,
    { timeout: PLAYER_BOOT_TIMEOUT_MS, polling: 500 },
  );
}

/** Presses a control at the point the report published for it. */
async function press(page: Page, control: Control, report: { canvasWidth: number; canvasHeight: number }): Promise<void> {
  const box = await page.locator("canvas").boundingBox();
  expect(box, "the player has no canvas element").not.toBeNull();

  const target = {
    x: box!.x + (control.x / report.canvasWidth) * box!.width,
    y: box!.y + ((report.canvasHeight - control.y) / report.canvasHeight) * box!.height,
  };

  await page.mouse.move(target.x, target.y);
  await page.waitForTimeout(200);
  await page.mouse.down();
  await page.waitForTimeout(200);
  await page.mouse.up();
}

/** Reads a control the overlay published, or fails saying which one is missing. */
function control(report: { buttons: Control[] }, name: string): Control {
  const found = report.buttons.find((button) => button.name === name);
  expect(found, `the overlay published no '${name}' control (it reported ${JSON.stringify(report.buttons)})`).toBeTruthy();
  return found!;
}

test.describe("ProjectSurvive's pause overlay in the browser", () => {
  test("stops the floor, resumes it, and returns to the camp with the damage taken", async ({ page }) => {
    test.setTimeout(330_000);
    const pageErrors = collectPageErrors(page);

    await page.goto("/?scene=Battle", { waitUntil: "domcontentloaded" });

    await waitForGlobal(page, SCENE);
    await expect
      .poll(() => readGlobal<string>(page, SCENE), { timeout: PLAYER_BOOT_TIMEOUT_MS, polling: 500 })
      .toBe("Battle");

    await waitForGlobal(page, PAUSE);
    await waitForGlobal(page, BATTLE);

    // The view binds before the scene's director attaches the floor, so the first document is
    // published with no strings and no HP. Wait for the one that has both rather than reading a
    // document that is still being built.
    await page.waitForFunction(
      (name) => {
        const report = (window as unknown as Record<string, unknown>)[name] as PauseReport | undefined;
        return Boolean(report && report.labels && report.labels.open);
      },
      PAUSE,
      { timeout: COMMAND_TIMEOUT_MS, polling: 250 },
    );

    // The floor has to be running before it can be frozen.
    await expect
      .poll(async () => Number((await readGlobal<BattleReport>(page, BATTLE)).stepsRun), {
        timeout: 60_000,
        polling: 1000,
      })
      .toBeGreaterThan(30);

    const closed = await readGlobal<PauseReport>(page, PAUSE);
    console.log("pause report (closed):", JSON.stringify(closed));

    expect(closed.scene).toBe("Battle");
    expect(closed.open).toBe(false);
    expect(closed.paused).toBe(false);
    expect(closed.lastError).toBe("");
    expect(closed.buttons.map((button) => button.name)).toEqual(["open"]);

    // The HUD's own caption is the localization table's, and it is Thai — the player has to be able
    // to read what the control does.
    expect(closed.labels.open).toMatch(/[\u0E00-\u0E7F]/);

    // Stage 13: the battle HUD's labels still fit, with the new control in the floor panel.
    await expectTextFits(page, "Battle");

    // --- หยุด ---------------------------------------------------------------
    await expect
      .poll(
        async () => {
          await press(page, control(closed, "open"), closed);
          return (await readGlobal<PauseReport>(page, PAUSE)).open;
        },
        { timeout: COMMAND_TIMEOUT_MS, polling: 500 },
      )
      .toBe(true);

    const paused = await readGlobal<PauseReport>(page, PAUSE);
    console.log("pause report (open):", JSON.stringify(paused));

    expect(paused.paused, "the overlay opened but the runner kept stepping").toBe(true);
    expect(paused.hp).toBeGreaterThan(0);
    expect(paused.labels.title).toMatch(/[\u0E00-\u0E7F]/);
    expect(paused.labels.warning).toMatch(/[\u0E00-\u0E7F]/);
    expect(paused.labels.resume).toMatch(/[\u0E00-\u0E7F]/);
    expect(paused.labels.leave).toMatch(/[\u0E00-\u0E7F]/);
    expect(paused.buttons.map((button) => button.name).sort()).toEqual(["leave", "resume"]);

    await page.screenshot({ path: "test-results/web-battle-pause.png", fullPage: false });

    // --- the floor is held still -------------------------------------------
    const frozenTick = Number((await readGlobal<PauseReport>(page, PAUSE)).tick);
    expect(frozenTick).toBeGreaterThan(0);

    await page.waitForTimeout(FREEZE_WINDOW_MS);

    const stillFrozen = await readGlobal<PauseReport>(page, PAUSE);
    expect(Number(stillFrozen.tick), "the floor kept stepping while the overlay was open").toBe(frozenTick);
    expect(stillFrozen.paused).toBe(true);

    // --- เล่นต่อ -------------------------------------------------------------
    await press(page, control(stillFrozen, "resume"), stillFrozen);

    await expect
      .poll(async () => (await readGlobal<PauseReport>(page, PAUSE)).open, {
        timeout: COMMAND_TIMEOUT_MS,
        polling: 500,
      })
      .toBe(false);

    // The runner steps again: its own document gets past the tick the overlay was frozen at. It is
    // rewritten every five seconds, so the budget is that cadence plus a margin.
    await expect
      .poll(async () => Number((await readGlobal<BattleReport>(page, BATTLE)).stepsRun), {
        timeout: 60_000,
        polling: 2000,
      })
      .toBeGreaterThan(frozenTick + 30);

    // --- กลับศาลา ------------------------------------------------------------
    const reopened = await readGlobal<PauseReport>(page, PAUSE);
    await expect
      .poll(
        async () => {
          await press(page, control(reopened, "open"), reopened);
          return (await readGlobal<PauseReport>(page, PAUSE)).open;
        },
        { timeout: COMMAND_TIMEOUT_MS, polling: 500 },
      )
      .toBe(true);

    const leaving = await readGlobal<PauseReport>(page, PAUSE);
    console.log("pause report (leaving):", JSON.stringify(leaving));

    await press(page, control(leaving, "leave"), leaving);

    await expect
      .poll(() => readGlobal<string>(page, SCENE), { timeout: HANDOVER_TIMEOUT_MS, polling: 500 })
      .toBe("Camp");

    await waitForGlobal(page, CAMP);
    const camp = await readGlobal<CampReport>(page, CAMP);
    console.log("camp report (after the retreat):", JSON.stringify(camp));

    expect(camp.flow).toBe("Camp");
    expect(camp.floor, "a retreat is not a clear, so the run is still on the floor it left").toBe(1);
    expect(camp.hp, "the damage taken on the floor was not carried into the camp").toBe(leaving.hp);
    expect(camp.bia).toBeGreaterThanOrEqual(0);
    expect(camp.canDepart, "the abandoned floor cannot be departed for again").toBe(true);

    await page.screenshot({ path: "test-results/web-battle-pause-camp.png", fullPage: false });

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });
});
