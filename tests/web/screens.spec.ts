import { expect, test, type Page } from "@playwright/test";

import { SCENE } from "./globals.mjs";

/**
 * Stage 09d item 4: screenshots of the **real WebGL player** at the two resolutions the GDD names
 * (1280×720 and 1920×1080) with `deviceScaleFactor` 1 and 2.
 *
 * This spec is the generator for the stage's evidence, not a gate: the stages whose Definition of
 * Done names a browser gate are 10, 12, 14 and 15 (AGENTS.md), so a plain `npx playwright test`
 * skips it (it would otherwise add thirteen player boots to every run). Regenerate with:
 *
 *   SCREENS=1 npx playwright test screens.spec.ts
 *
 * Two things make the shots honest:
 *
 * - The **canvas element** is resized, not the browser window. Unity's default desktop template
 *   pins the canvas' CSS size at 960×600, so a 1920×1080 *viewport* still renders 960×600 and is
 *   upscaled by the browser — measured again here, in the `default` shot below, which is the only
 *   one that leaves the canvas as the template ships it. The canvas element is the size path a
 *   player actually has (the template's fullscreen button, a HiDPI backing store).
 * - The images are captured at `scale: "device"`, so a deviceScaleFactor 2 shot is the framebuffer
 *   the display really gets, not a downscaled copy of it. The player's own `canvasWidth` is logged
 *   for each shot: Unity answers a 1920 CSS-wide canvas at deviceScaleFactor 2 with a 3840-wide
 *   backing store, so the UI is drawn at two device pixels per CSS pixel, and the log says so.
 */

const CAMP = "__projectSurviveCamp";
const GEOMETRY = "__projectSurviveCampGeometry";
const BATTLE = "__projectSurviveBattle";

const PLAYER_BOOT_TIMEOUT_MS = 180_000;

/** Relative to this directory (Playwright runs with `tests/web` as its working directory). */
const OUT = "../../artifacts/stage09d/screens";

interface Geometry {
  canvasWidth: number;
  canvasHeight: number;
  buttons: { name: string; x: number; y: number }[];
  rects: { name: string; layer: string }[];
}

interface Box {
  x: number;
  y: number;
  width: number;
  height: number;
}

interface Point {
  x: number;
  y: number;
}

const SIZES = [
  { width: 1280, height: 720 },
  { width: 1920, height: 1080 },
];

const SCALES = [1, 2];

test.describe("Stage 09d screenshots", () => {
  test.skip(process.env.SCREENS !== "1", "the screenshot spec runs on request: SCREENS=1");

  for (const size of SIZES) {
    for (const scale of SCALES) {
      test(`the camp at ${size.width}x${size.height}, deviceScaleFactor ${scale}`, async ({ browser }) => {
        const { context, page } = await open(browser, size, scale);
        try {
          await enterCamp(page);
          await setCanvasSize(page, size.width, size.height);
          await waitForCanvasWidth(page, size.width * scale);

          const at = `${size.width}x${size.height}`;
          const suffix = `${at}-dsf${scale}`;
          const closed = await geometry(page);
          console.log(
            `camp ${suffix}: the player adopted ${closed.canvasWidth}x${closed.canvasHeight} device pixels ` +
              `for a ${at} CSS canvas (deviceScaleFactor ${scale}), ${closed.rects.length} published regions`,
          );

          await shoot(page, `${OUT}/web-camp-${suffix}.png`);

          // --- the จัดย่าม panel, opened the way a player opens it ------------------------------
          await clickUntilOpen(page);

          await shoot(page, `${OUT}/web-camp-panel-${suffix}.png`);
        } finally {
          await context.close();
        }
      });
    }
  }

  /**
   * The one shot that leaves the canvas as the template ships it: a 1920×1080 *viewport* around a
   * 960×600 canvas, which is what a player who never presses the template's fullscreen button sees.
   * It is the "before" half of the sharpness question — the browser upscales 960×600 to the screen.
   */
  test("the camp at a 1920x1080 viewport with the template's own 960x600 canvas", async ({ browser }) => {
    const { context, page } = await open(browser, { width: 1920, height: 1080 }, 1);
    try {
      await enterCamp(page);

      const box = await canvasBox(page);
      const layout = await geometry(page);
      console.log(
        `camp default: a 1920x1080 viewport, a canvas of ${box.width}x${box.height} CSS pixels, ` +
          `the player drawing ${layout.canvasWidth}x${layout.canvasHeight} pixels`,
      );

      await shoot(page, `${OUT}/web-camp-1920x1080-viewport-template-canvas.png`);
    } finally {
      await context.close();
    }
  });

  for (const size of SIZES) {
    for (const scale of SCALES) {
      test(`the title screen at ${size.width}x${size.height}, deviceScaleFactor ${scale}`, async ({ browser }) => {
        const { context, page } = await open(browser, size, scale);
        try {
          await page.goto("/?scene=Title", { waitUntil: "domcontentloaded" });
          await waitForScene(page, "Title");
          await setCanvasSize(page, size.width, size.height);
          await page.waitForTimeout(1500);

          await shoot(page, `${OUT}/web-title-${size.width}x${size.height}-dsf${scale}.png`);
        } finally {
          await context.close();
        }
      });

      test(`the battle HUD at ${size.width}x${size.height}, deviceScaleFactor ${scale}`, async ({ browser }) => {
        const { context, page } = await open(browser, size, scale);
        try {
          await page.goto("/?scene=Battle", { waitUntil: "domcontentloaded" });
          await waitForScene(page, "Battle");
          await setCanvasSize(page, size.width, size.height);

          // A battle that has run long enough to have drawn its wave, its timeline and its spells.
          // The report is the signal, not a wall-clock guess — and its numbers are written as
          // strings, so they are read as strings and converted.
          await expect
            .poll(
              async () =>
                Number(
                  (await readGlobal<{ tick?: string } | null>(page, BATTLE))?.tick ?? "0",
                ),
              { timeout: 90_000, polling: 500 },
            )
            .toBeGreaterThan(300);

          await shoot(page, `${OUT}/web-battle-${size.width}x${size.height}-dsf${scale}.png`);
        } finally {
          await context.close();
        }
      });
    }
  }
});

async function open(
  browser: import("@playwright/test").Browser,
  size: { width: number; height: number },
  scale: number,
): Promise<{ context: import("@playwright/test").BrowserContext; page: Page }> {
  const context = await browser.newContext({
    viewport: { width: size.width, height: size.height },
    deviceScaleFactor: scale,
  });

  return { context, page: await context.newPage() };
}

async function shoot(page: Page, path: string): Promise<void> {
  // scale: "device" - a deviceScaleFactor 2 shot is the framebuffer the display gets.
  await page.locator("canvas").screenshot({ path, scale: "device" });
}

async function readGlobal<T>(page: Page, name: string): Promise<T> {
  return page.evaluate((globalName) => (window as unknown as Record<string, unknown>)[globalName] as T, name);
}

async function waitForScene(page: Page, scene: string): Promise<void> {
  await expect
    .poll(() => readGlobal<string>(page, SCENE), { timeout: PLAYER_BOOT_TIMEOUT_MS, polling: 500 })
    .toBe(scene);
}

async function enterCamp(page: Page): Promise<void> {
  await page.goto("/?scene=Camp", { waitUntil: "domcontentloaded" });
  await waitForScene(page, "Camp");
  await expect
    .poll(async () => Boolean(await readGlobal<unknown>(page, GEOMETRY)), { timeout: PLAYER_BOOT_TIMEOUT_MS, polling: 500 })
    .toBe(true);
}

function geometry(page: Page): Promise<Geometry> {
  return readGlobal<Geometry>(page, GEOMETRY);
}

/**
 * Resizes the player's canvas element — the size path a player has (see the header).
 *
 * The one-second settle is the same one `camp.spec.ts` uses: the player adopts the new size and
 * re-maps pointer coordinates a moment after the element changed.
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

  await page.waitForTimeout(1000);
}

async function waitForCanvasWidth(page: Page, width: number): Promise<void> {
  await expect
    .poll(async () => (await geometry(page)).canvasWidth, { timeout: 15_000, polling: 250 })
    .toBe(width);
}

async function canvasBox(page: Page): Promise<Box> {
  const box = await page.locator("canvas").boundingBox();
  expect(box, "the player has no canvas element").not.toBeNull();
  return box!;
}

/** Unity screen pixels (origin bottom-left) → page coordinates inside the canvas element. */
function toPage(box: Box, layout: Geometry, unity: Point): Point {
  return {
    x: box.x + (unity.x / layout.canvasWidth) * box.width,
    y: box.y + ((layout.canvasHeight - unity.y) / layout.canvasHeight) * box.height,
  };
}

function control(layout: Geometry, name: string): Point {
  const button = layout.buttons.find((candidate) => candidate.name === name);
  expect(button, `the layout report has no '${name}' control`).toBeDefined();
  return { x: button!.x, y: button!.y };
}

/**
 * Opens จัดย่าม, retrying the click.
 *
 * A synthesised click into the player is occasionally lost — measured in Stage 09 (3 of 4 runs for
 * a control clicked after a canvas resize) and again by this spec, which lost it in three of the
 * four configurations on its first run. This spec is not asserting that rule — `camp.spec.ts` does
 * — so it clicks again, which is what a player does.
 */
async function clickUntilOpen(page: Page): Promise<void> {
  for (let attempt = 1; attempt <= 4; attempt += 1) {
    const layout = await geometry(page);
    const point = toPage(await canvasBox(page), layout, control(layout, "openBag"));

    await page.mouse.move(point.x, point.y);
    await page.waitForTimeout(200);
    await page.mouse.down();
    await page.waitForTimeout(200);
    await page.mouse.up();

    const opened = await expect
      .poll(async () => (await readGlobal<{ bagVisible: boolean }>(page, CAMP)).bagVisible, {
        timeout: 5_000,
        polling: 250,
      })
      .toBe(true)
      .then(
        () => true,
        () => false,
      );

    if (opened) {
      await page.waitForTimeout(500);
      return;
    }

    console.log(`the จัดย่าม click was not registered (attempt ${attempt} of 4)`);

    // Off the control and a pause before trying again: the player has to see the pointer leave and
    // come back for the press that follows to be its own press.
    await page.mouse.move(point.x, point.y - 60);
    await page.waitForTimeout(800);
  }
}
