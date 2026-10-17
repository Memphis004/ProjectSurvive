/**
 * The shapes of the documents the player publishes on `window`, as the specs read them.
 *
 * These live in a `.ts` file rather than in `globals.mjs` on purpose: `globals.mjs` is imported by
 * plain node (`scripts/probe-boot.mjs`), so it must stay JavaScript — a TypeScript interface in it is
 * a parse error under that loader. The names themselves stay in `globals.mjs`, which remains the one
 * place a page global is written down.
 *
 * The fields mirror the C# report classes exactly (`TitleReport`, `HubReport`, `EncounterReport`,
 * `ResultReport`); a field added there and not here is a spec that silently stops asserting on it.
 */

/** A control the screen published a point for. `x`/`y` are canvas pixels, origin bottom-left. */
export interface Control {
  name: string;
  x: number;
  y: number;
}

/** The part of every screen report a spec may rely on. */
export interface ScreenReport {
  scene: string;
  canvasWidth: number;
  canvasHeight: number;
  buttons: Control[];
}

/** `__projectSurviveTitle`. */
export interface TitleReport extends ScreenReport {
  startError: string;
  title: string;
  startLabel: string;
  diagnostics: string;
  ready: boolean;
}

/** `__projectSurviveHub`. */
export interface HubReport extends ScreenReport {
  title: string;
  instructions: string;
  encountersLine: string;
  departLabel: string;
  finishLabel: string;
}

/** `__projectSurviveEncounter`. */
export interface EncounterReport extends ScreenReport {
  title: string;
  progressLine: string;
  winLabel: string;
  loseLabel: string;
}

/** `__projectSurviveResult`. */
export interface ResultReport extends ScreenReport {
  startError: string;
  outcome: string;
  finished: boolean;
  encounters: number;
  seed: string;
  headline: string;
  summaryLine: string;
  seedLine: string;
  restartLabel: string;
  titleLabel: string;
}
