import { expect, test, type Page } from "@playwright/test";

import { AUDIO, SCENE } from "./globals.mjs";

/**
 * Stage 13c-1 acceptance in the browser: the built player has sound, and the sound it
 * makes is the one the game asked for.
 *
 * The player publishes its own audio document (`AudioDirector`), because a WebGL build
 * cannot run Unity's test framework: what a browser test can observe is what the player
 * said about itself.
 *
 *   __projectSurviveAudio - voices, totalPlays, stolen, throttled, missingClips,
 *                           musicPlaying, musicCue, sfxVolume, musicVolume, cueCounts
 *
 * Two claims, one per scene:
 *   - the arena plays the battle track and lands hit sounds during a floor;
 *   - the camp plays the camp track.
 *
 * What this cannot prove is that a *human* hears anything: the browser test says the
 * source was started and the clip was found, not that the speakers are on.
 */

/** Boot: download + compile the player, then load a scene. */
const PLAYER_BOOT_TIMEOUT_MS = 180_000;

/** A floor's first damage lands within a few seconds of the battle starting. */
const CUE_TIMEOUT_MS = 60_000;

interface AudioReport {
  voices: number;
  totalPlays: number;
  stolen: number;
  throttled: number;
  missingClips: number;
  musicPlaying: boolean;
  musicCue: string;
  sfxVolume: number;
  musicVolume: number;
  cueCounts: Record<string, number>;
}

function collectPageErrors(page: Page): string[] {
  const errors: string[] = [];
  page.on("console", (message) => {
    if (message.type() === "error") {
      errors.push(`console.error: ${message.text()}`);
    }

    // Unity logs this one as a plain warning, so it never reaches the error check above. It means a
    // source is playing with no listener: music that is "on" and unheard. Stage 13c-1 measured it
    // in a batch-mode PlayMode run, where the audio device is absent, and this is the player-side
    // guard against the same condition shipping.
    if (message.text().includes("no audio listeners")) {
      errors.push(`console.warn: ${message.text()}`);
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

async function readAudio(page: Page): Promise<AudioReport> {
  return page.evaluate(
    (globalName) => (window as unknown as Record<string, unknown>)[globalName] as AudioReport,
    AUDIO,
  );
}

/**
 * A user gesture. Browsers refuse to start an AudioContext until the page has been
 * interacted with, which is why the game never plays anything during Boot: the first
 * track starts on the scene the player walks into after pressing a control.
 */
async function pressTheCanvas(page: Page): Promise<void> {
  const canvas = page.locator("canvas").first();
  await canvas.waitFor({ state: "visible", timeout: PLAYER_BOOT_TIMEOUT_MS });
  const box = await canvas.boundingBox();
  expect(box, "the player has no canvas").not.toBeNull();
  await page.mouse.click(box!.x + box!.width / 2, box!.y + box!.height / 2);
}

test.describe("ProjectSurvive sound in the browser", () => {
  test("the arena plays its own track and a floor makes hit sounds", async ({ page }) => {
    test.setTimeout(330_000);
    const pageErrors = collectPageErrors(page);

    await page.goto("/?scene=Battle", { waitUntil: "domcontentloaded" });

    await waitForGlobal(page, SCENE);
    await expect
      .poll(() => page.evaluate((n) => (window as unknown as Record<string, unknown>)[n], SCENE), {
        timeout: PLAYER_BOOT_TIMEOUT_MS,
      })
      .toBe("Battle");

    await waitForGlobal(page, AUDIO);
    await pressTheCanvas(page);

    const start = await readAudio(page);
    console.log("audio report (battle start):", JSON.stringify(start));

    // The pool is authored, not grown: eight voices and one music source, and not one
    // cue was missing its clip.
    expect(start.voices).toBe(8);
    expect(start.missingClips).toBe(0);
    expect(start.musicCue).toBe("MusicBattle");

    // The floor lands damage within seconds; the cue is the event, not a timer. Poll the
    // hit cue itself: the music is already a play, so `totalPlays` would pass on its own.
    await expect
      .poll(async () => (await readAudio(page)).cueCounts["sfx-hit"] ?? 0, {
        timeout: CUE_TIMEOUT_MS,
        polling: 500,
      })
      .toBeGreaterThan(0);

    const playing = await readAudio(page);
    console.log("audio report (battle, after cues):", JSON.stringify(playing));

    expect(playing.cueCounts["sfx-hit"], "a floor that dealt damage played no hit sound").toBeGreaterThan(0);
    expect(playing.musicCue).toBe("MusicBattle");
    expect(playing.musicPlaying, "the battle track was configured but the source never started").toBe(true);

    // Voice stealing is a fact, not a failure: a burst may take a playing voice. It is
    // reported so that a mix that is always saturated is visible.
    console.log(
      `voices: ${playing.voices}, plays: ${playing.totalPlays}, stolen: ${playing.stolen}, ` +
        `throttled: ${playing.throttled}`,
    );

    await page.screenshot({ path: "test-results/web-audio-battle.png", fullPage: false });

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });

  test("the camp plays the camp track", async ({ page }) => {
    test.setTimeout(330_000);
    const pageErrors = collectPageErrors(page);

    await page.goto("/?scene=Camp", { waitUntil: "domcontentloaded" });

    await waitForGlobal(page, SCENE);
    await expect
      .poll(() => page.evaluate((n) => (window as unknown as Record<string, unknown>)[n], SCENE), {
        timeout: PLAYER_BOOT_TIMEOUT_MS,
      })
      .toBe("Camp");

    await waitForGlobal(page, AUDIO);
    await pressTheCanvas(page);

    await expect
      .poll(async () => (await readAudio(page)).musicCue, { timeout: CUE_TIMEOUT_MS, polling: 500 })
      .toBe("MusicCamp");

    const camp = await readAudio(page);
    console.log("audio report (camp):", JSON.stringify(camp));

    expect(camp.musicPlaying).toBe(true);
    expect(camp.missingClips).toBe(0);

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });
});
