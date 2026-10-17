import { expect, test, type Page } from "@playwright/test";

import { HUB, PLAYER_BOOT_TIMEOUT_MS, SCENE, SMOKE, TITLE } from "./globals.mjs";
import type { Control, HubReport, TitleReport } from "./report-types";

/**
 * The title screen, in a built player: **the screen and its start control**.
 *
 * The gap this spec closes is the same one the original camp spec closed for the game: a title
 * screen nothing can leave is a dead end. The player publishes where its start control is
 * (`__projectSurviveTitle`), this spec clicks that point, and what it asserts on afterwards is the
 * game's own answer — the scene marker becoming `Hub` and the Hub's own report showing a run that
 * has cleared no encounters yet and can depart.
 *
 * Documents this spec reads, all on `window`:
 *
 *   __projectSurviveSmoke   - the boot self-check (the handover to the title is what it reports)
 *   __projectSurviveScene   - the scene on screen
 *   __projectSurviveTitle   - what the title screen shows, and where its start control is
 *   __projectSurviveHub     - the Hub's own report, after the press
 *
 * Timing: the first load of a Unity WebGL player downloads and compiles the WASM, so the boot budget
 * is minutes. Everything after it is quick — a click, a fade and a scene load.
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

/**
 * Resizes the player's **canvas element** — the size path a player has, because the template's own
 * control and a HiDPI backing store both change the element rather than the window. Unity's default
 * desktop template pins the canvas' CSS size at 960×600, which is why the window is not the lever.
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
  // element changed, which is what a click needs rather than the layout alone (a click ~300 ms after
  // a resize was swallowed in two runs of four, measured in Stage 09).
  await page.waitForTimeout(1000);
}

test.describe("the template's title screen in the browser", () => {
  test("starts a run from the control the screen draws", async ({ page }) => {
    test.setTimeout(330_000);
    const pageErrors = collectPageErrors(page);

    await page.goto("/", { waitUntil: "domcontentloaded" });
    await waitForGlobal(page, SMOKE);

    // The boot scene hands over to the title and stops there — the screen change is the player's own.
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

    // The two strings the screen draws are the localization table's, and both are Thai: the caption is
    // the one the player presses, so "it rendered" is a real assertion here.
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

    // The template's own player size, which is what the fixture starts at.
    expect(title.canvasWidth, "the fixture starts at the template's own canvas size").toBe(960);

    // The control is centred on the canvas: the CanvasScaler's virtual width follows the canvas' aspect,
    // so a corner-anchored control would sit 26 px right of 480 at this size (measured in Stage 10d).
    expect(
      Math.abs(start.x - title.canvasWidth / 2),
      "the control is not where the canvas centre is at the template's own size",
    ).toBeLessThanOrEqual(2);

    // Now the GDD's second size, taken the way a player takes it: the size *and* the point it aims a
    // click at. A document that never settles is a failure — the first one written after a resize is a
    // frame early (`TitleSceneDirector` rewrites until the geometry agrees with the size it published).
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
    expect(Math.abs(live.x - resized!.canvasWidth / 2), "the control is not where the canvas centre is").toBeLessThanOrEqual(2);

    await page.screenshot({ path: "test-results/web-title.png", fullPage: false });

    // The click, at the point the screen published — synthesised clicks into the player are
    // occasionally lost, so it is retried the way a player would.
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
        .toBe("Hub")
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

    expect(left, "the title screen never left for the Hub: the start control did not start a run").toBe(true);

    // The Hub's own report: a run that has cleared nothing yet and has both ways out.
    await waitForGlobal(page, HUB);
    const hub = await readGlobal<HubReport>(page, HUB);
    console.log("hub report (after the title):", JSON.stringify(hub));

    expect(hub.scene).toBe("Hub");
    expect(hub.encountersLine).toContain("0");
    expect(hub.departLabel).toMatch(/[\u0E00-\u0E7F]/);
    expect(hub.finishLabel).toMatch(/[\u0E00-\u0E7F]/);
    expect(hub.buttons.map((button) => button.name).sort()).toEqual(["depart", "finish"]);

    await page.screenshot({ path: "test-results/web-hub.png", fullPage: false });

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });
});
