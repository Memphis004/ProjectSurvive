import { expect, test, type Page } from "@playwright/test";

/**
 * Stage 08 acceptance in the browser: "เล่นชั้น 1 จนชนะหรือแพ้ได้ใน Web".
 *
 * The Battle scene publishes its own progress document on `window`, the same way the
 * Stage 03 self-check does, because a WebGL player cannot run Unity's test framework:
 * what a browser test can observe is what the player published about itself.
 *
 *   __projectSurviveBattle - scene, phase, tick, stepsRun, droppedTicks, speed,
 *                            viewsCreated / viewsGrownDuringPlay / viewsBound /
 *                            poolCapacity, damageNumbers, timelineMarkers,
 *                            droppedEvents, hp / mp / bia / kills / waveIndex,
 *                            and — once the floor is over — outcome / endReason / finalHp
 *
 * The numeric fields the writer emits as strings (tick, stepsRun, …) are read through
 * `Number(...)`: JSON does not know they were numbers.
 *
 * Timing: the first load of a Unity WebGL player downloads and compiles the WASM, and
 * floor 1 is sixty seconds of play at 1×. The timeouts below are minutes on purpose; a
 * shorter one would report a slow machine as a broken game.
 */

const BATTLE = "__projectSurviveBattle";
const SCENE = "__projectSurviveScene";

/** Boot: download + compile the player. */
const PLAYER_BOOT_TIMEOUT_MS = 180_000;

/** Floor 1 is 60 s at 1×, plus the poll interval and the tail of the build's boot. */
const FLOOR_TIMEOUT_MS = 150_000;

interface BattleReport {
  scene: string;
  phase: string;
  tick: string | number;
  stepsRun: string | number;
  droppedTicks: string | number;
  speed: number;
  viewsCreated: number;
  viewsGrownDuringPlay: number;
  viewsBound: number;
  poolCapacity: number;
  damageNumbers: number;
  timelineMarkers: number;
  droppedEvents: string | number;
  ended: boolean;
  hp?: number;
  maxHp?: number;
  mp?: number;
  bia?: number;
  kills?: number;
  waveIndex?: number;
  remainingSeconds?: number;
  outcome?: string;
  endReason?: string;
  finalHp?: number;
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

async function waitForGlobal(page: Page, name: string): Promise<void> {
  await page.waitForFunction(
    (globalName) => Boolean((window as unknown as Record<string, unknown>)[globalName]),
    name,
    { timeout: PLAYER_BOOT_TIMEOUT_MS, polling: 500 },
  );
}

async function readBattle(page: Page): Promise<BattleReport> {
  return page.evaluate(
    (globalName) => (window as unknown as Record<string, unknown>)[globalName] as BattleReport,
    BATTLE,
  );
}

test.describe("ProjectSurvive floor 1 in the browser", () => {
  test("plays the floor from the scene boot to a win or a loss", async ({ page }) => {
    test.setTimeout(330_000);
    const pageErrors = collectPageErrors(page);

    await page.goto("/?scene=Battle", { waitUntil: "domcontentloaded" });

    // The scene override is what puts the player straight into the arena; if it ever
    // stopped working this is where it shows, before any of the battle assertions.
    await waitForGlobal(page, SCENE);
    await expect.poll(() => page.evaluate((n) => (window as unknown as Record<string, unknown>)[n], SCENE), {
      timeout: PLAYER_BOOT_TIMEOUT_MS,
    }).toBe("Battle");

    await waitForGlobal(page, BATTLE);

    const started = await readBattle(page);
    console.log("battle report (start):", JSON.stringify(started));

    expect(started.scene).toBe("Battle");
    expect(started.phase).toBe("Running");
    expect(started.poolCapacity).toBeGreaterThan(1000);
    expect(started.timelineMarkers).toBe(1);
    expect(Number(started.stepsRun)).toBe(0);

    // Play the floor out. The player stands still, which is a real playthrough: the
    // floor is survived or it is not, and either outcome is the acceptance.
    await expect
      .poll(async () => (await readBattle(page)).ended, { timeout: FLOOR_TIMEOUT_MS, polling: 2000 })
      .toBe(true);

    const finished = await readBattle(page);
    console.log("battle report (end):", JSON.stringify(finished));

    expect(["Won", "Lost"]).toContain(finished.outcome);
    expect(finished.endReason).toBeTruthy();
    expect(Number(finished.stepsRun)).toBeGreaterThan(0);
    expect(Number(finished.tick)).toBeGreaterThan(0);
    expect(finished.finalHp).toBeGreaterThanOrEqual(0);
    expect(finished.finalHp).toBeLessThanOrEqual(finished.maxHp ?? 0);

    // Requirement 7: nothing is instantiated after the pool is warmed. A non-zero here
    // is a floor that outgrew its pool, which in a browser is a frame spike.
    expect(finished.viewsGrownDuringPlay).toBe(0);
    expect(finished.viewsBound).toBeGreaterThan(0);

    await page.screenshot({ path: "test-results/web-battle-end.png", fullPage: false });

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });

  test("a mid-floor report shows a battle in progress", async ({ page }) => {
    test.setTimeout(330_000);
    const pageErrors = collectPageErrors(page);

    await page.goto("/?scene=Battle", { waitUntil: "domcontentloaded" });
    await waitForGlobal(page, BATTLE);

    // The runner reports every five seconds, so a second report is the mid-floor one:
    // it is what proves the container is actually stepping rather than having published
    // a single frame at Begin.
    await expect
      .poll(async () => Number((await readBattle(page)).stepsRun), { timeout: FLOOR_TIMEOUT_MS, polling: 1000 })
      .toBeGreaterThan(30);

    const midFloor = await readBattle(page);
    console.log("battle report (mid floor):", JSON.stringify(midFloor));

    expect(midFloor.scene).toBe("Battle");
    expect(midFloor.phase).toBe("Running");
    expect(Number(midFloor.stepsRun)).toBeGreaterThan(30);
    expect(midFloor.hp).toBeGreaterThan(0);
    expect(midFloor.maxHp).toBeGreaterThan(0);
    expect(midFloor.bia).toBeGreaterThanOrEqual(0);
    expect(midFloor.viewsBound).toBeGreaterThan(0);
    expect(midFloor.viewsGrownDuringPlay).toBe(0);

    // A dropped tick is a long frame, not a lost floor: the runner counts what it threw
    // away rather than replaying it, and the run must still be progressing.
    expect(Number(midFloor.stepsRun)).toBeGreaterThan(Number(midFloor.droppedTicks));

    await page.screenshot({ path: "test-results/web-battle-mid.png", fullPage: false });

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });
});
