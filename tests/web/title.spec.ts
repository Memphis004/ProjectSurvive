import { expect, test, type Page } from "@playwright/test";

import { AUDIO, PERSISTENCE, SCENE, SETTINGS, SMOKE } from "./globals.mjs";
import { expectTextFits } from "./textfit.mjs";

/**
 * Stage 10d's first leg, in a built player: **the title screen and its start control**.
 *
 * The gap this spec closes is stated in the stage's own report: Stage 10's loop was Boot → Title →
 * Camp → … and nothing on the title screen could leave it, so the suite could open the game and look
 * at it and nothing more. A player had the same problem. The player publishes where its start control
 * is (`__projectSurviveTitle`), this spec clicks that point, and what it asserts on afterwards is the
 * game's own answer: the scene marker becoming `Camp` and the camp's own report showing a run at floor
 * 1 that can depart.
 *
 * Documents this spec reads, all on `window`:
 *
 *   __projectSurviveSmoke   - the boot self-check (the handover to the title is what it reports)
 *   __projectSurviveScene   - the scene on screen
 *   __projectSurviveTitle   - what the title screen shows, and where its start control is
 *   __projectSurviveCamp    - the camp's own report, after the press
 *
 * Timing: the first load of a Unity WebGL player downloads and compiles the WASM, so the boot budget
 * is minutes. Everything after it is quick — a click, a fade and a scene load.
 */

const TITLE = "__projectSurviveTitle";
const CAMP = "__projectSurviveCamp";

/** Boot: download + compile the player. */
const PLAYER_BOOT_TIMEOUT_MS = 180_000;

/** How long a scene handover (a fade plus a load) may take. */
const HANDOVER_TIMEOUT_MS = 30_000;

interface Control {
  name: string;
  x: number;
  y: number;
}

interface TitleReport {
  scene: string;
  startError: string;
  title: string;
  startLabel: string;
  diagnostics: string;
  ready: boolean;
  canvasWidth: number;
  canvasHeight: number;
  buttons: Control[];
}

interface CampReport {
  flow: string;
  floor: number;
  totalFloors: number;
  hp: number;
  bia: number;
  canDepart: boolean;
}

/** How long a press may take to reach the published report. */
const COMMAND_TIMEOUT_MS = 20_000;

/** `__projectSurviveSettings` (Stage 13c-2a). */
interface SettingsReport {
  open: boolean;
  canvasWidth: number;
  canvasHeight: number;
  volume: number;
  music: number;
  sfx: number;
  labels: { volume: string; music: string; sfx: string; language: string; speed: string };
  buttons: Control[];
}

/** ``__projectSurvivePersistence`` — only the fields this spec reads. */
interface PersistenceReport {
  musicVolume: number;
  sfxVolume: number;
}

/** ``__projectSurviveAudio`` — only what proves the device took the level. */
interface AudioReport {
  musicVolume: number;
  sfxVolume: number;
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

/**
 * Resizes the player's **canvas element** — the size path a player has, because the template's own
 * fullscreen control and a HiDPI backing store both change the element rather than the window. Unity's
 * default desktop template pins the canvas' CSS size at 960×600, which is why the window is not the
 * lever here (measured with a throwaway probe for `camp.spec.ts`).
 */
async function setCanvasSize(page: Page, width: number, height: number): Promise<void> {
  await page.evaluate(
    (size) => {
      const canvas = document.querySelector("canvas") as HTMLCanvasElement;
      canvas.style.width = `${size.width}px`;
      canvas.style.height = `${size.height}px`;
      window.dispatchEvent(new Event("resize"));
    },
    { width, height },
  );

  // The player adopts the new size — and re-maps pointer coordinates to it — a moment after the
  // element changed, which is what a click needs rather than the layout alone (the same measurement
  // `camp.spec.ts` records: a click ~300 ms after a resize was swallowed in two runs of four).
  await page.waitForTimeout(1000);
}

test.describe("ProjectSurvive's title screen in the browser", () => {
  test("starts a run from the control the screen draws", async ({ page }) => {
    test.setTimeout(330_000);
    const pageErrors = collectPageErrors(page);

    await page.goto("/", { waitUntil: "domcontentloaded" });
    await waitForGlobal(page, SMOKE);

    // The boot scene hands over to the title and stops there — the screen change is the player's own,
    // not something this spec asked for.
    await waitForGlobal(page, SCENE);
    await expect
      .poll(() => readGlobal<string>(page, SCENE), { timeout: PLAYER_BOOT_TIMEOUT_MS, polling: 500 })
      .toBe("Title");

    await waitForGlobal(page, TITLE);
    const title = await readGlobal<TitleReport>(page, TITLE);
    console.log("title report:", JSON.stringify(title));

    expect(title.scene).toBe("Title");
    expect(title.startError).toBe("");
    expect(title.ready).toBe(true);

    // Stage 13: every label on this screen — the settings panel's included — fits its rectangle
    // and is drawn by the shipped font. The player measured it; this reads its document.
    await expectTextFits(page, "Title");

    // The two strings the screen draws are the localization table's, and both are Thai: the caption is
    // the one the player presses, so "it rendered" is a real assertion here (Stage 09d's Thai floor).
    expect(title.title).toMatch(/[\u0E00-\u0E7F]/);
    expect(title.startLabel).toMatch(/[\u0E00-\u0E7F]/);
    expect(title.diagnostics, "the build/config line is a developer's, so it is not Thai").toContain("build");

    // One control, named, with a point inside the canvas.
    expect(title.buttons.map((button) => button.name)).toEqual(["start"]);
    const start = title.buttons[0];
    expect(start.x).toBeGreaterThan(0);
    expect(start.y).toBeGreaterThan(0);
    expect(start.x).toBeLessThanOrEqual(title.canvasWidth);
    expect(start.y).toBeLessThanOrEqual(title.canvasHeight);

    // The template's own player size, which is what the report was written for before anything moved.
    expect(title.canvasWidth, "the fixture starts at the template's own canvas size").toBe(960);

    // The control is centred on the canvas, and this size is where a *corner*-anchored control would
    // have given itself away: the CanvasScaler's virtual width follows the canvas' aspect, so at
    // 960×600 a control placed by 1920-wide corner arithmetic sits 505.964 — 26 px right of 480 —
    // while the two labels above it, which are centre-anchored, stay centred. That is the reading this
    // assertion got the first time it ran in the player, and it is why the scene now anchors the
    // control to the canvas' centre (`ProjectSetup.CreateTitleScene`).
    expect(
      Math.abs(start.x - title.canvasWidth / 2),
      "the control is not where the canvas centre is at the template's own size",
    ).toBeLessThanOrEqual(2);

    // Now the GDD's second size, taken the way a player takes it. The document has to keep up, and
    // "keeps up" is about both halves of it: the size it names *and* the point it aims a click at. So
    // what is polled is the point, at the new size — not the size alone. The difference matters and
    // was measured: the player reports the new canvas size in the frame the element changed, while
    // Unity lays its widgets out of that size during the same frame's render, so the first document
    // written after a resize is a frame early (`TitleSceneDirector` rewrites until the geometry it
    // publishes agrees with the size it published). A document that never settles is still a failure
    // here — before that fix the point stayed at 505.964, the 960×600 layout, while `canvasWidth`
    // already said 1280, and 505.964 is 134 px from the 640 asserted below.
    await setCanvasSize(page, 1280, 720);
    const resized = await expect
      .poll(
        async () => {
          const report = await readGlobal<TitleReport>(page, TITLE);
          return report.canvasWidth === 1280 && report.buttons.length === 1
            ? Math.round(report.buttons[0].x)
            : null;
        },
        { timeout: 15_000, polling: 250 },
      )
      .toBe(640)
      .then(
        () => readGlobal<TitleReport>(page, TITLE),
        () => null,
      );

    expect(
      resized,
      "the title screen did not republish its control's position after the canvas was resized",
    ).not.toBeNull();
    expect(resized!.canvasHeight).toBe(720);
    expect(resized!.startError).toBe("");
    expect(resized!.buttons.map((button) => button.name)).toEqual(["start"]);

    const live = resized!.buttons[0];
    expect(live.x).toBeGreaterThan(0);
    expect(live.y).toBeGreaterThan(0);
    expect(live.x).toBeLessThanOrEqual(resized!.canvasWidth);
    expect(live.y).toBeLessThanOrEqual(resized!.canvasHeight);

    // The control is centred, and 1280×720 is the reference aspect, so the point is the canvas' own
    // centre exactly — 640 (960/2 = 480 was the same statement at the fixture's size).
    expect(Math.abs(live.x - resized!.canvasWidth / 2), "the control is not where the canvas centre is").toBeLessThanOrEqual(2);

    await page.screenshot({ path: "test-results/web-title.png", fullPage: false });

    // The click, at the point the screen published — a synthesised click into the player is
    // occasionally lost (measured in Stage 09), so it is retried the way a player would.
    const box = await page.locator("canvas").boundingBox();
    expect(box, "the player has no canvas element").not.toBeNull();

    const target = {
      x: box!.x + (live.x / resized!.canvasWidth) * box!.width,
      y: box!.y + ((resized!.canvasHeight - live.y) / resized!.canvasHeight) * box!.height,
    };

    let left = false;
    for (let attempt = 1; attempt <= 4 && !left; attempt += 1) {
      await page.mouse.move(target.x, target.y);
      await page.waitForTimeout(200);
      await page.mouse.down();
      await page.waitForTimeout(200);
      await page.mouse.up();

      left = await expect
        .poll(() => readGlobal<string>(page, SCENE), { timeout: 10_000, polling: 500 })
        .toBe("Camp")
        .then(
          () => true,
          () => false,
        );

      if (!left) {
        console.log(`the 'start' click was not registered (attempt ${attempt} of 4)`);
        await page.mouse.move(target.x, target.y - 60);
        await page.waitForTimeout(800);
      }
    }

    expect(left, "the title screen never left for the camp: เริ่มเล่น did not start a run").toBe(true);

    // The camp's own report: a run that began at floor 1, with the starting purse and a control it can
    // depart from — the run the title started, not one the camp built for the occasion.
    await waitForGlobal(page, CAMP);
    const camp = await readGlobal<CampReport>(page, CAMP);
    console.log("camp report (after the title):", JSON.stringify(camp));

    expect(camp.flow).toBe("Camp");
    expect(camp.floor).toBe(1);
    expect(camp.canDepart).toBe(true);
    expect(camp.hp).toBeGreaterThan(0);
    expect(camp.bia).toBeGreaterThan(0);

    await page.screenshot({ path: "test-results/web-title-camp.png", fullPage: false });

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });

  test("opens the settings panel and moves the music and effects buses", async ({ page }) => {
    test.setTimeout(330_000);
    const pageErrors = collectPageErrors(page);

    await page.goto("/", { waitUntil: "domcontentloaded" });
    await waitForGlobal(page, SCENE);
    await expect
      .poll(() => readGlobal<string>(page, SCENE), { timeout: PLAYER_BOOT_TIMEOUT_MS, polling: 500 })
      .toBe("Title");

    await waitForGlobal(page, SETTINGS);

    const closed = await readGlobal<SettingsReport>(page, SETTINGS);
    console.log("settings report (closed):", JSON.stringify(closed));

    // The panel is shut on arrival and only the control that opens it is pressable.
    expect(closed.open).toBe(false);
    expect(closed.buttons.map((button) => button.name)).toEqual(["open"]);
    expect(closed.music).toBeGreaterThan(0);
    expect(closed.music).toBeLessThanOrEqual(1);
    expect(closed.sfx).toBeGreaterThan(0);

    // Labelled with the localized formats the presenter fills, not with a value the view invented.
    for (const key of ["volume", "music", "sfx"] as const) {
      expect(closed.labels[key], `the '${key}' row has no label`).toContain("%");
    }
    expect(closed.labels.music).toMatch(/[\u0E00-\u0E7F]/);

    const canvas = await page.locator("canvas").boundingBox();
    expect(canvas, "the player has no canvas element").not.toBeNull();

    // Report pixels are Unity screen pixels with the origin at the bottom-left; the page's are CSS
    // pixels with the origin at the top-left. Every click below converts through the report's own
    // canvas size and the canvas element's box — never a coordinate guessed from a screenshot.
    const clickControl = async (report: SettingsReport, name: string) => {
      const control = report.buttons.find((candidate) => candidate.name === name);
      expect(control, `the report has no '${name}' control`).toBeDefined();
      await page.mouse.click(
        canvas!.x + (control!.x / report.canvasWidth) * canvas!.width,
        canvas!.y + ((report.canvasHeight - control!.y) / report.canvasHeight) * canvas!.height,
      );
    };

    await clickControl(closed, "open");

    await expect
      .poll(async () => (await readGlobal<SettingsReport>(page, SETTINGS)).open, {
        timeout: COMMAND_TIMEOUT_MS,
        polling: 250,
      })
      .toBe(true);

    const opened = await readGlobal<SettingsReport>(page, SETTINGS);
    console.log("settings report (open):", JSON.stringify(opened));

    const rows = ["close", "volumeDown", "volumeUp", "musicDown", "musicUp", "sfxDown", "sfxUp"];
    for (const name of rows) {
      expect(opened.buttons.map((button) => button.name), `no '${name}' control was published`).toContain(name);
    }

    // A step down on the music bus. The report is polled rather than assumed, because the press
    // reaches the game on the next frame and the number is the game's own.
    await clickControl(opened, "musicDown");
    await expect
      .poll(async () => (await readGlobal<SettingsReport>(page, SETTINGS)).music, {
        timeout: COMMAND_TIMEOUT_MS,
        polling: 250,
      })
      .toBeLessThan(opened.music);

    const afterMusic = await readGlobal<SettingsReport>(page, SETTINGS);
    expect(afterMusic.sfx, "the effects bus must not move with the music").toBe(opened.sfx);
    expect(afterMusic.volume, "the master must not move with a bus").toBe(opened.volume);

    // The device took the level: the audio document's music volume follows the panel's. Polled, because
    // the director publishes its document on its own quarter-second beat.
    await waitForGlobal(page, AUDIO);
    await expect
      .poll(
        async () => Math.abs((await readGlobal<AudioReport>(page, AUDIO)).musicVolume - afterMusic.music) < 0.001,
        { timeout: COMMAND_TIMEOUT_MS, polling: 250 },
      )
      .toBe(true);

    // One step down on the effects bus, then storage, which is the only thing that survives a reload.
    await clickControl(afterMusic, "sfxDown");
    await expect
      .poll(async () => (await readGlobal<SettingsReport>(page, SETTINGS)).sfx, {
        timeout: COMMAND_TIMEOUT_MS,
        polling: 250,
      })
      .toBeLessThan(afterMusic.sfx);

    const afterSfx = await readGlobal<SettingsReport>(page, SETTINGS);
    await waitForGlobal(page, PERSISTENCE);
    await expect
      .poll(
        async () => {
          const persisted = await readGlobal<PersistenceReport>(page, PERSISTENCE);
          return (
            Math.abs(persisted.musicVolume - afterSfx.music) < 0.001 &&
            Math.abs(persisted.sfxVolume - afterSfx.sfx) < 0.001
          );
        },
        { timeout: COMMAND_TIMEOUT_MS, polling: 250 },
      )
      .toBe(true);

    await page.screenshot({ path: "test-results/web-title-settings.png", fullPage: false });

    // Closing hides the rows again, so a click there would land on whatever is behind the panel.
    await clickControl(afterSfx, "close");
    await expect
      .poll(async () => (await readGlobal<SettingsReport>(page, SETTINGS)).open, {
        timeout: COMMAND_TIMEOUT_MS,
        polling: 250,
      })
      .toBe(false);

    const reclosed = await readGlobal<SettingsReport>(page, SETTINGS);
    expect(reclosed.buttons.map((button) => button.name)).toEqual(["open"]);

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });
});
