import { expect, test, type Page } from "@playwright/test";

import { SCENE } from "./globals.mjs";
import { expectTextFits } from "./textfit.mjs";

/**
 * Stage 10's last leg, in a built player: **the summary screen**.
 *
 * Stage 10's report recorded the gap this spec closes. The browser suite's floor 1 is *won* by a
 * standing player, so a run that reached the summary was never observed in a WebGL build — and
 * IL2CPP stripping is exactly the class of defect a Mono test cannot see ("the summary screen's DI
 * survives a real build" was NOT RUN). A surrender is not an input the suite has, so the floor is
 * ended the way a developer ends one: through the dev menu, which the Development build the suite
 * runs shows and its floor accepts (`DevCheats.Allowed` is `Debug.isDebugBuild || Application.isEditor`,
 * and a release build does neither — `DevMenuReportTests` asserts that state on the shipped scene).
 *
 * The player publishes three documents this spec reads, all on `window`:
 *
 *   __projectSurviveBattle    - the arena's progress, including `ended` / `outcome` / `finalHp`
 *   __projectSurviveDevMenu   - where the dev panel's controls are, so a click can land on one
 *   __projectSurviveResult    - what the summary screen shows, and where its two controls are
 *   __projectSurviveCamp      - the camp's own report, read after the player presses Restart
 *
 * Timing: the first load of a Unity WebGL player downloads and compiles the WASM, so the boot budget
 * is minutes. Everything after the boot is quick — a floor that is ended on purpose, a fade and a
 * scene load.
 */

const BATTLE = "__projectSurviveBattle";
const DEV_MENU = "__projectSurviveDevMenu";
const RESULT = "__projectSurviveResult";
const CAMP = "__projectSurviveCamp";

/** Boot: download + compile the player. */
const PLAYER_BOOT_TIMEOUT_MS = 180_000;

/** How long the floor may take to end after the click that ends it. */
const FLOOR_END_TIMEOUT_MS = 60_000;

/** How long a scene handover (a fade plus a load) may take. */
const HANDOVER_TIMEOUT_MS = 30_000;

/** A control as the page reports it: screen pixels with the origin at the bottom-left. */
interface Control {
  name: string;
  x: number;
  y: number;
}

interface ControlReport {
  canvasWidth: number;
  canvasHeight: number;
  buttons: Control[];
}

interface DevMenuReport extends ControlReport {
  scene: string;
  visible: boolean;
}

interface BattleReport {
  scene: string;
  phase: string;
  ended: boolean;
  outcome?: string;
  endReason?: string;
  finalHp?: number;
  maxHp?: number;
  kills?: number;
  bia?: number;
}

interface SummaryReport extends ControlReport {
  scene: string;
  startError: string;
  outcome: string;
  finished: boolean;
  victory: boolean;
  floor: number;
  totalFloors: number;
  floorsCleared: number;
  kills: number;
  bia: number;
  hp: number;
  maxHp: number;
  seed: string;
  headline: string;
  floorLine: string;
  killsLine: string;
  biaLine: string;
  hpLine: string;
  restartLabel: string;
  titleLabel: string;
}

interface CampReport {
  flow: string;
  floor: number;
  totalFloors: number;
  hp: number;
  bia: number;
  canDepart: boolean;
}

interface Box {
  x: number;
  y: number;
  width: number;
  height: number;
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
    .poll(() => readGlobal<string>(page, SCENE), { timeout: PLAYER_BOOT_TIMEOUT_MS, polling: 500 })
    .toBe(scene);
}

async function canvasBox(page: Page): Promise<Box> {
  const box = await page.locator("canvas").boundingBox();
  expect(box, "the player has no canvas element").not.toBeNull();
  return box!;
}

/** Unity screen pixels (origin bottom-left) → page coordinates inside the canvas element. */
function toPage(box: Box, report: ControlReport, control: Control): { x: number; y: number } {
  return {
    x: box.x + (control.x / report.canvasWidth) * box.width,
    y: box.y + ((report.canvasHeight - control.y) / report.canvasHeight) * box.height,
  };
}

function control(report: ControlReport, name: string): Control {
  const found = report.buttons?.find((candidate) => candidate.name === name);
  expect(found, `the report has no '${name}' control: ${JSON.stringify(report.buttons)}`).toBeDefined();
  return found!;
}

/**
 * Presses one control, retrying the click.
 *
 * A synthesised click into the player is occasionally lost — measured in Stage 09 (3 of 4 runs for a
 * control clicked after a canvas resize) and recorded again by `screens.spec.ts`. This spec is not
 * asserting that rule (the camp's own suite does), so it clicks again, which is what a player does:
 * off the control, a pause, and back.
 */
async function clickUntil(
  page: Page,
  report: ControlReport,
  name: string,
  because: string,
): Promise<void> {
  const point = toPage(await canvasBox(page), report, control(report, name));

  for (let attempt = 1; attempt <= 4; attempt += 1) {
    await page.mouse.move(point.x, point.y);
    await page.waitForTimeout(200);
    await page.mouse.down();
    await page.waitForTimeout(200);
    await page.mouse.up();

    const landed = await expect
      .poll(async () => (await readGlobal<BattleReport>(page, BATTLE))?.ended ?? false, {
        timeout: 8_000,
        polling: 250,
      })
      .toBe(true)
      .then(
        () => true,
        () => false,
      );

    if (landed) {
      return;
    }

    console.log(`the '${name}' click was not registered (attempt ${attempt} of 4)`);
    await page.mouse.move(point.x, point.y - 60);
    await page.waitForTimeout(800);
  }

  throw new Error(`${because}: '${name}' was never accepted`);
}

test.describe("ProjectSurvive's summary screen in the browser", () => {
  test("a lost floor shows the run's summary and Restart starts a new run", async ({ page }) => {
    // A lost floor is produced through the dev menu's เล่นต่อ→แพ้ทันที control, and a release build
    // has no dev menu (by design, Stage 14 item 7). `tools/ci.ps1` and `tools/test.ps1 -Release`
    // set PROJECTSURVIVE_BUILD=release, and this case skips itself there instead of waiting two
    // minutes for a panel that must never appear. It is covered against a Development build.
    test.skip(
      process.env.PROJECTSURVIVE_BUILD === "release",
      "the dev menu does not exist in a release build",
    );

    test.setTimeout(330_000);
    const pageErrors = collectPageErrors(page);

    // A floor played on its own: the scene builds and departs a run by itself, which is what makes
    // this check independent of a full run through five floors.
    await page.goto("/?scene=Battle", { waitUntil: "domcontentloaded" });
    await waitForScene(page, "Battle");
    await waitForGlobal(page, BATTLE);

    const started = await readGlobal<BattleReport>(page, BATTLE);
    console.log("battle report (start):", JSON.stringify(started));
    expect(started.phase).toBe("Running");

    // The dev menu's own document. Its absence means the build does not allow dev cheats, which is a
    // configuration this check cannot run in rather than a defect — `tools/build-web.ps1` builds
    // Development by default, and that is what the suite's web steps run.
    await waitForGlobal(page, DEV_MENU);
    const menu = await readGlobal<DevMenuReport>(page, DEV_MENU);
    console.log("dev menu report:", JSON.stringify(menu));

    expect(menu.scene).toBe("Battle");
    expect(menu.visible, "the dev panel is on screen, so the report says where its controls are").toBe(true);
    expect(menu.buttons.length).toBe(6);

    await clickUntil(page, menu, "loseNow", "the floor could never be ended on purpose");
    await expect
      .poll(async () => (await readGlobal<BattleReport>(page, BATTLE)).ended, {
        timeout: FLOOR_END_TIMEOUT_MS,
        polling: 500,
      })
      .toBe(true);

    const finished = await readGlobal<BattleReport>(page, BATTLE);
    console.log("battle report (end):", JSON.stringify(finished));

    expect(finished.outcome).toBe("Lost");
    expect(finished.finalHp).toBe(0);
    expect(Number(finished.kills)).toBeGreaterThanOrEqual(0);

    // The run ended, so the arena leaves for the summary screen by itself — the handover is the
    // player's own, not something this spec asked for.
    await expect
      .poll(() => readGlobal<string>(page, SCENE), { timeout: HANDOVER_TIMEOUT_MS, polling: 500 })
      .toBe("Result");

    await waitForGlobal(page, RESULT);
    const summary = await readGlobal<SummaryReport>(page, RESULT);
    console.log("summary report:", JSON.stringify(summary));

    expect(summary.scene).toBe("Result");
    expect(summary.startError).toBe("");
    expect(summary.finished).toBe(true);

    // Stage 13: the summary screen's Thai text fits and the font can draw it.
    await expectTextFits(page, "Result");
    expect(summary.outcome).toBe("Defeat");
    expect(summary.victory).toBe(false);
    expect(summary.floor).toBe(1);
    expect(summary.floorsCleared).toBe(0);
    expect(summary.totalFloors).toBeGreaterThan(1);
    expect(summary.seed).toBeTruthy();

    // The summary summarises **the run that was played**: the arena's own last report and the screen
    // agree on what it ended with. That is the half a Mono test cannot see in a build — the numbers
    // crossed IL2CPP.
    expect(summary.kills).toBe(Number(finished.kills));
    expect(summary.hp).toBe(finished.finalHp);
    expect(summary.maxHp).toBe(finished.maxHp);

    // The purse is deliberately *not* compared with the arena's own `bia`: that field is the arena's
    // earned-currency counter (`hud.Currency`, `BattleSimulationRunner.PublishReport`), and the summary
    // prints the **run's** purse (`RunSession.Wallet.Bia`, `ResultReport`). They are two different
    // numbers — in a real run the first is usually 0 because no drop column pays yet — and asserting
    // they are equal was this spec's own mistake, measured in the built player (arena 0, purse 6). What
    // is asserted instead is what the screen can be held to: the purse is a real number and the line
    // drawn from it carries it.

    // Every line the screen draws is non-empty and carries the run's own numbers.
    for (const [name, line] of Object.entries({
      headline: summary.headline,
      floorLine: summary.floorLine,
      killsLine: summary.killsLine,
      biaLine: summary.biaLine,
      hpLine: summary.hpLine,
      restartLabel: summary.restartLabel,
      titleLabel: summary.titleLabel,
    })) {
      expect(line, `${name} is empty`).toBeTruthy();
    }

    // The headline is the localized defeat line, and it is Thai: the summary is the one screen whose
    // whole content is text, so "it rendered" is a real assertion here (Stage 09d's Thai floor).
    expect(summary.headline).toMatch(/[\u0E00-\u0E7F]/);
    expect(summary.floorLine).toContain(String(summary.floor));
    expect(summary.bia).toBeGreaterThanOrEqual(0);
    expect(summary.biaLine).toContain(String(summary.bia));
    expect(summary.killsLine).toContain(String(summary.kills));
    expect(summary.restartLabel).toMatch(/[\u0E00-\u0E7F]/);
    expect(summary.titleLabel).toMatch(/[\u0E00-\u0E7F]/);

    // Both ways out are named, with a point to click.
    expect(summary.buttons.map((button) => button.name).sort()).toEqual(["restart", "title"]);
    for (const button of summary.buttons) {
      expect(button.x).toBeGreaterThan(0);
      expect(button.y).toBeGreaterThan(0);
      expect(button.x).toBeLessThanOrEqual(summary.canvasWidth);
      expect(button.y).toBeLessThanOrEqual(summary.canvasHeight);
    }

    await page.screenshot({ path: "test-results/web-result-summary.png", fullPage: false });

    // The player's own way out: press Restart, which is the control the scene drew (not a command
    // this spec sent), and the camp comes up with a run it can depart from — a run that starts at
    // full HP, where the run that just ended had none.
    const restart = control(summary, "restart");
    const restartPoint = toPage(await canvasBox(page), summary, restart);

    for (let attempt = 1; attempt <= 4; attempt += 1) {
      await page.mouse.move(restartPoint.x, restartPoint.y);
      await page.waitForTimeout(200);
      await page.mouse.down();
      await page.waitForTimeout(200);
      await page.mouse.up();

      const left = await expect
        .poll(() => readGlobal<string>(page, SCENE), { timeout: 10_000, polling: 500 })
        .toBe("Camp")
        .then(
          () => true,
          () => false,
        );

      if (left) {
        break;
      }

      console.log(`the 'restart' click was not registered (attempt ${attempt} of 4)`);
      await page.mouse.move(restartPoint.x, restartPoint.y - 60);
      await page.waitForTimeout(800);

      expect(attempt, "the summary screen never left for the camp").toBeLessThan(4);
    }

    await waitForGlobal(page, CAMP);
    const camp = await readGlobal<CampReport>(page, CAMP);
    console.log("camp report (after Restart):", JSON.stringify(camp));

    expect(camp.flow).toBe("Camp");
    expect(camp.floor).toBe(1);
    expect(camp.canDepart).toBe(true);
    expect(camp.hp, "a restarted run starts at full HP, and the run that ended had none").toBeGreaterThan(summary.hp);
    expect(camp.hp).toBe(summary.maxHp);

    await page.screenshot({ path: "test-results/web-result-camp-restart.png", fullPage: false });

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });
});
