/**
 * The page globals every spec reads — defined once, here (Stage 10t).
 *
 * The C# side has one point of the same names (`WebGlBridge`), which passes them
 * to the `.jslib` bridge; a rename on that side must be made here as well, and
 * this file is the only place on the browser-test side that a name is written
 * down. Every `*.spec.ts` imports from it, and so does
 * `scripts/probe-boot.mjs`, so a re-export cannot leave one reader looking at a
 * global nobody publishes any more.
 *
 * Plain ESM rather than `.ts` because the diagnostic script is plain node: a
 * `.mjs` file is what both a Playwright spec and `node scripts/probe-boot.mjs`
 * can import without a build step.
 *
 *   __projectSurviveSmoke        the boot self-check report (`SceneBoot`)
 *   __projectSurviveScene        the scene on screen (every scene director)
 *   __projectSurviveThaiSpike    the Thai text spike measurement (`ThaiSpikeReporter`)
 *   __projectSurvivePersistence  the checkpoint/settings storage verdict (`GameSave`)
 */
export const SMOKE = "__projectSurviveSmoke";
export const SCENE = "__projectSurviveScene";
export const THAI_SPIKE = "__projectSurviveThaiSpike";
export const PERSISTENCE = "__projectSurvivePersistence";
