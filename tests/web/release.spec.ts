import { expect, test, type Page } from "@playwright/test";

import { SCENE, SMOKE } from "./globals.mjs";

/**
 * Stage 14, brief item 7: **a release build has no harness API and no dev menu.**
 *
 * This spec only makes sense against a Release build, and `tools/test.ps1` only adds it for one
 * (`-Release`). Against a Development build it would fail by design, because that build does have
 * both — which is the same statement read from the other side, and why `harness.spec.ts` exists.
 *
 * The claim is stronger than "the menu is hidden": `HarnessBridge` and the one line that installs
 * it are compiled out of a Release player, so there is no page global, no send function and no
 * command a page could call even knowing the names. `HarnessBridgeTests` pins the compile gate in
 * the source; this file proves it in the artifact that would actually ship.
 */

const HARNESS = "__projectSurviveHarness";
const HARNESS_SEND = "__projectSurviveHarnessSend";
const HARNESS_SNAPSHOT = "__projectSurviveHarnessSnapshot";
const DEV_MENU = "__projectSurviveDevMenu";
const BATTLE = "__projectSurviveBattle";

const PLAYER_BOOT_TIMEOUT_MS = 180_000;

interface BattleReport {
  scene: string;
  phase: string;
  stepsRun: string | number;
  ended: boolean;
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

async function readGlobal(page: Page, name: string): Promise<unknown> {
  return page.evaluate((globalName) => (window as unknown as Record<string, unknown>)[globalName], name);
}

test.describe("ProjectSurvive release build", () => {
  test("a release player boots without the harness", async ({ page }) => {
    test.setTimeout(240_000);
    const pageErrors = collectPageErrors(page);

    await page.goto("/", { waitUntil: "domcontentloaded" });

    // The player really loaded: the boot self-check passed and the title is on screen. Without
    // this first assert, "no harness global" would also be true of a blank page.
    await page.waitForFunction(
      (name) => {
        const report = (window as unknown as Record<string, unknown>)[name] as
          | { passed?: boolean }
          | undefined;
        return report?.passed === true;
      },
      SMOKE,
      { timeout: PLAYER_BOOT_TIMEOUT_MS, polling: 250 },
    );

    await expect
      .poll(() => readGlobal(page, SCENE), { timeout: PLAYER_BOOT_TIMEOUT_MS, polling: 250 })
      .toBe("Title");

    expect(await readGlobal(page, HARNESS), "a release build published the harness document").toBeUndefined();
    expect(
      await page.evaluate((name) => typeof (window as unknown as Record<string, unknown>)[name], HARNESS_SEND),
      "a release build defined the harness send function",
    ).toBe("undefined");

    // And a page that guesses the name still has nothing to talk to.
    const enqueued = await page.evaluate((name) => {
      const send = (window as unknown as Record<string, unknown>)[name];
      return typeof send === "function";
    }, HARNESS_SEND);
    expect(enqueued).toBe(false);

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });

  test("a release arena has no dev menu", async ({ page }) => {
    test.setTimeout(240_000);
    const pageErrors = collectPageErrors(page);

    await page.goto("/?scene=Battle", { waitUntil: "domcontentloaded" });

    await page.waitForFunction(
      (name) => Boolean((window as unknown as Record<string, unknown>)[name]),
      BATTLE,
      { timeout: PLAYER_BOOT_TIMEOUT_MS, polling: 250 },
    );

    // The floor has to be *playing* for the menu to have had its chance to appear: the report is
    // published when the panel becomes measurable, which happens on the first frame a cheats-on
    // build shows it. Waiting on ticks rather than on the clock makes that a real condition.
    await expect
      .poll(
        async () =>
          Number(
            ((await readGlobal(page, BATTLE)) as BattleReport | undefined)?.stepsRun ?? 0,
          ),
        { timeout: 120_000, polling: 500 },
      )
      .toBeGreaterThan(30);

    const battle = (await readGlobal(page, BATTLE)) as BattleReport;
    expect(battle.phase).toBe("Running");

    expect(await readGlobal(page, DEV_MENU), "a release build published the dev menu").toBeUndefined();
    expect(await readGlobal(page, HARNESS_SNAPSHOT), "a release build published a harness snapshot").toBeUndefined();

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });
});
