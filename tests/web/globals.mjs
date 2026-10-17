/**
 * The page globals every spec reads — defined once, here (Stage 10t).
 *
 * The C# side has one point of the same names (`WebGlBridge`, and one report class per
 * screen), which passes them to the `.jslib` bridge; a rename on that side must be made
 * here as well, and this file is the only place on the browser-test side that a name is
 * written down. Every `*.spec.ts` imports from it, and so does
 * `scripts/probe-boot.mjs`, so a re-export cannot leave one reader looking at a global
 * nobody publishes any more.
 *
 * Plain ESM rather than `.ts` because the diagnostic script is plain node: a `.mjs` file
 * is what both a Playwright spec and `node scripts/probe-boot.mjs` can import without a
 * build step.
 *
 *   __projectSurviveSmoke      the boot self-check report (`SceneBoot`)
 *   __projectSurviveScene      the scene on screen (every scene director)
 *   __projectSurviveThaiSpike  the Thai text spike measurement (`ThaiSpikeReporter`)
 *   __projectSurviveTitle      the title screen, and where its start control is
 *   __projectSurviveHub        the Hub screen, and where its two controls are
 *   __projectSurviveEncounter  the Encounter screen, and where its two controls are
 *   __projectSurviveResult     the summary screen, and where its two controls are
 */
export const SMOKE = "__projectSurviveSmoke";
export const SCENE = "__projectSurviveScene";
export const THAI_SPIKE = "__projectSurviveThaiSpike";
export const TITLE = "__projectSurviveTitle";
export const HUB = "__projectSurviveHub";
export const ENCOUNTER = "__projectSurviveEncounter";
export const RESULT = "__projectSurviveResult";

/** How long a player may take to download, compile and boot. */
export const PLAYER_BOOT_TIMEOUT_MS = 180_000;

/** How long a scene handover (a fade plus a load) may take. */
export const HANDOVER_TIMEOUT_MS = 30_000;

