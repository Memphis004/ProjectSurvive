import { expect, test, type Page } from "@playwright/test";

import {
  HARNESS,
  HARNESS_DIAGNOSTICS,
  HARNESS_OPS,
  HARNESS_SEND,
  HARNESS_SNAPSHOT,
  SCENE,
  SMOKE,
} from "./globals.mjs";

/**
 * Stage 14: the development harness, driven from the page.
 *
 * These are the **bridge-driven** tests, kept apart from the UI tests on purpose (brief item 3):
 * everything else in this folder presses a control the player drew, while this file sends the two
 * commands a page cannot express otherwise — start a run with a named seed, and advance a floor by
 * a known number of ticks. Both go through the same use case the screen uses (`TitlePresenter.
 * RequestStart` and `IBattleSession.Step`), so a pass here is evidence about the real run and the
 * real session, not about a copy of them.
 *
 * It is a **development-only** file: `release.spec.ts` asserts the harness is absent, and
 * `tools/test.ps1` only runs this spec against a Development build.
 *
 * The harness never uses a fixed sleep as a wait: `__projectSurviveHarness.ready` is the handshake
 * (published after the config is loaded and the first scene is up), and every command's answer is
 * read back from the document the player republishes.
 */

interface HarnessDoc {
  ready: boolean;
  scene: string;
  unityVersion: string;
  appVersion: string;
  isEditor: boolean;
  configHash: string;
  seed: number;
  hasRun: boolean;
  flow: string;
  sendGlobal: string;
  battleAvailable: boolean;
  commandCount: number;
  lastCommand: string;
  lastResult: string;
  lastError: string;
  commands: string[];
}

interface HarnessSnapshot {
  scene: string;
  phase: string;
  tick: string | number;
  stepsRun: string | number;
  ended: boolean;
  harnessDriven: boolean;
  ticksAdvanced: number;
  tickBefore: string | number;
  outcome?: string;
  endReason: string;
  finalHp?: number;
}

interface HarnessDiagnostics {
  unityVersion: string;
  appVersion: string;
  platform: string;
  isDevelopmentBuild: boolean;
  configHash: string;
  scene: string;
  seed: number;
  hasRun: boolean;
  flow: string;
  floorIndex: number;
}

const PLAYER_BOOT_TIMEOUT_MS = 180_000;
const COMMAND_TIMEOUT_MS = 60_000;

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

async function readDoc(page: Page): Promise<HarnessDoc | undefined> {
  return page.evaluate(
    (name) => (window as unknown as Record<string, unknown>)[name] as HarnessDoc | undefined,
    HARNESS,
  );
}

/**
 * Waits for the player's own ready handshake — not a timer. `ready` is published once the config
 * has been loaded and a scene is on screen, and republished as the scene changes.
 */
async function waitForHarness(page: Page, expectedScene?: string): Promise<HarnessDoc> {
  await page.waitForFunction(
    ({ name, scene }) => {
      const doc = (window as unknown as Record<string, unknown>)[name] as HarnessDoc | undefined;
      if (!doc || doc.ready !== true || !doc.configHash) {
        return false;
      }
      return scene ? doc.scene === scene : true;
    },
    { name: HARNESS, scene: expectedScene ?? "" },
    { timeout: PLAYER_BOOT_TIMEOUT_MS, polling: 250 },
  );

  const doc = await readDoc(page);
  expect(doc, "the harness document disappeared after its handshake").toBeTruthy();
  return doc as HarnessDoc;
}

/** Sends one command and waits until the player has answered it. */
async function sendCommand(
  page: Page,
  opcode: number,
  argument: number,
  expectedCommand: string,
): Promise<HarnessDoc> {
  const before = (await readDoc(page))?.commandCount ?? 0;

  const accepted = await page.evaluate(
    ({ sendName, op, arg }) => {
      const send = (window as unknown as Record<string, unknown>)[sendName] as
        | ((opcode: number, argument: number) => boolean)
        | undefined;
      return typeof send === "function" ? send(op, arg) : false;
    },
    { sendName: HARNESS_SEND, op: opcode, arg: argument },
  );

  expect(accepted, `the page could not enqueue ${expectedCommand}`).toBe(true);

  // Waiting on the counter, not on a string: an unknown opcode is answered with an empty
  // `lastCommand`, so "the document says command X" cannot express "it answered at all".
  await page.waitForFunction(
    ({ name, count }) => {
      const doc = (window as unknown as Record<string, unknown>)[name] as HarnessDoc | undefined;
      return Boolean(doc) && doc!.commandCount > count;
    },
    { name: HARNESS, count: before },
    { timeout: COMMAND_TIMEOUT_MS, polling: 100 },
  );

  const doc = await readDoc(page);
  expect(doc, "the harness document disappeared while answering a command").toBeTruthy();
  if (expectedCommand) {
    expect(doc!.lastCommand).toBe(expectedCommand);
  }
  return doc as HarnessDoc;
}

async function readSnapshot(page: Page): Promise<HarnessSnapshot | undefined> {
  return page.evaluate(
    (name) => (window as unknown as Record<string, unknown>)[name] as HarnessSnapshot | undefined,
    HARNESS_SNAPSHOT,
  );
}

test.describe("ProjectSurvive development harness", () => {
  test("a named seed starts the real run through the title's own use case", async ({ page }) => {
    test.setTimeout(240_000);
    const pageErrors = collectPageErrors(page);

    await page.goto("/", { waitUntil: "domcontentloaded" });

    // Title is where the seeded start belongs: it is the screen whose departure the command
    // performs, through the same presenter the เริ่มเล่น control calls.
    const ready = await waitForHarness(page, "Title");
    expect(ready.commands).toEqual([
      "startSeededRun",
      "advanceTicks",
      "snapshot",
      "diagnostics",
    ]);
    expect(ready.battleAvailable).toBe(false);
    expect(ready.seed).not.toBe(0);
    expect(ready.unityVersion).toBeTruthy();

    const seed = 20261016;
    const afterStart = await sendCommand(page, HARNESS_OPS.startSeededRun, seed, "startSeededRun");
    expect(afterStart.lastResult, afterStart.lastError).toBe("accepted");
    expect(afterStart.seed).toBe(seed);
    expect(afterStart.hasRun).toBe(true);

    // The run really started: the title's director moves to the camp, which is the same
    // handover a press produces.
    await page.waitForFunction(
      (name) => (window as unknown as Record<string, unknown>)[name] === "Camp",
      SCENE,
      { timeout: COMMAND_TIMEOUT_MS, polling: 250 },
    );

    const atCamp = await waitForHarness(page, "Camp");
    expect(atCamp.flow).toBe("Camp");
    expect(atCamp.seed).toBe(seed);

    // Diagnostics: the artifact a failure is read from later.
    await sendCommand(page, HARNESS_OPS.diagnostics, 0, "diagnostics");
    const diagnostics = await page.evaluate(
      (name) => (window as unknown as Record<string, unknown>)[name] as HarnessDiagnostics,
      HARNESS_DIAGNOSTICS,
    );
    expect(diagnostics.configHash).toBeTruthy();
    expect(diagnostics.unityVersion).toBeTruthy();
    expect(diagnostics.isDevelopmentBuild).toBe(true);
    expect(diagnostics.seed).toBe(seed);
    expect(diagnostics.flow).toBe("Camp");

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });

  test("advancing a known number of ticks plays the floor and names its end", async ({ page }, testInfo) => {
    test.setTimeout(240_000);
    const pageErrors = collectPageErrors(page);

    // `?scene=Battle` is the diagnostic handover Stage 08 documented: a standalone floor with a
    // real run and real tables. It is NOT counted as combat coverage of the camp→battle flow —
    // battle.spec.ts owns that — it is the fastest honest way to reach a session.
    await page.goto("/?scene=Battle", { waitUntil: "domcontentloaded" });

    const ready = await waitForHarness(page, "Battle");
    expect(ready.battleAvailable).toBe(true);

    // The floor starts stepping on the frame it begins, so a tick or two may already have run
    // before the first command is picked up — and the frame loop may step between two commands.
    // What has to be exact is what *the command* did, which the snapshot reports atomically:
    // `tickBefore` and `ticksAdvanced`.

    // A first advance, small enough that the floor cannot have ended.
    const mid = await sendCommand(page, HARNESS_OPS.advanceTicks, 300, "advanceTicks");
    expect(mid.lastError, mid.lastResult).toBe("");
    expect(mid.lastResult).toBe("advanced:300");

    const midSnapshot = await readSnapshot(page);
    expect(midSnapshot, "advanceTicks published no snapshot").toBeTruthy();
    expect(midSnapshot!.phase).toBe("Running");
    expect(midSnapshot!.endReason).toBe("None");
    expect(midSnapshot!.ticksAdvanced).toBe(300);
    expect(Number(midSnapshot!.tick) - Number(midSnapshot!.tickBefore)).toBe(300);
    expect(midSnapshot!.harnessDriven).toBe(true);

    // Then enough ticks to reach the end of floor 1 (1800 ticks at 30 Hz, plus the sweep).
    const end = await sendCommand(page, HARNESS_OPS.advanceTicks, 2000, "advanceTicks");
    expect(end.lastError, end.lastResult).toBe("");
    expect(end.lastResult).toMatch(/^advanced:\d+$/);

    const finished = await readSnapshot(page);
    console.log("harness snapshot (end):", JSON.stringify(finished));
    expect(finished, "the final advance published no snapshot").toBeTruthy();
    expect(finished!.ended).toBe(true);
    expect(finished!.phase).toBe("Won");
    expect(finished!.outcome).toBe("Won");
    expect(finished!.endReason).toBe("SurvivedAllWaves");
    expect(Number(finished!.tick)).toBeGreaterThanOrEqual(1800);

    // The same answer the UI-driven battle spec reaches in real time — reached through the
    // session, which is what makes it a tick-for-tick statement rather than a wall-clock one.
    await testInfo.attach("harness-snapshot.json", {
      body: JSON.stringify(finished, null, 2),
      contentType: "application/json",
    });

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });

  test("the harness refuses what it does not own", async ({ page }) => {
    test.setTimeout(240_000);
    const pageErrors = collectPageErrors(page);

    await page.goto("/", { waitUntil: "domcontentloaded" });
    await waitForHarness(page, "Title");

    // No floor on the title: an advance is refused by name, not silently ignored.
    const advance = await sendCommand(page, HARNESS_OPS.advanceTicks, 30, "advanceTicks");
    expect(advance.lastResult).toBe("refused");
    expect(advance.lastError).toBe("harness.no_battle");

    // A bypass command is not in the vocabulary at all. 99 is "finish the floor" in no build:
    // the harness has deliberately never answered one.
    const unknown = await sendCommand(page, 99, 0, "");
    expect(unknown.lastResult).toBe("refused");
    expect(unknown.lastError).toBe("harness.unknown_command");

    // The smoke verdict is still the boot's, so this ran against a player that booted properly.
    const smoke = await page.evaluate(
      (name) => (window as unknown as Record<string, unknown>)[name] as { passed?: boolean } | undefined,
      SMOKE,
    );
    expect(smoke?.passed).toBe(true);

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });
});
