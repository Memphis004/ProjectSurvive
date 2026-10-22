import { expect } from "@playwright/test";

import { TEXT_FIT } from "./globals.mjs";

/**
 * Stage 13: the player's own text audit, asserted from the page.
 *
 * Every scene publishes `__projectSurviveTextFit` — one row per label it drew, measured inside the
 * player with the shipped font asset: the rectangle it was authored into, the size it actually
 * needs, whether that overflows, and the characters the font cannot draw. This helper waits for
 * the document of the screen being tested and fails with the offending labels named, so a Thai
 * string that grew past its box or a character the baked atlas never received is a test failure
 * rather than something a reviewer has to notice in a screenshot.
 */
export async function expectTextFits(page, scene, { timeout = 60_000 } = {}) {
  await page.waitForFunction(
    ({ name, wanted }) => {
      const report = window[name];
      return Boolean(report && report.scene === wanted && report.labelCount > 0);
    },
    { name: TEXT_FIT, wanted: scene },
    { timeout, polling: 250 },
  );

  const report = await page.evaluate((name) => window[name], TEXT_FIT);

  expect(report.canvasWidth, `${scene} published no canvas size`).toBeGreaterThan(0);
  expect(report.scene).toBe(scene);

  const bad = (report.labels ?? []).filter(
    (label) => label.overflows === true || (label.missingGlyphs ?? "").length > 0,
  );

  expect(
    bad,
    `${scene}: text does not fit or the font cannot draw it\n` +
      bad
        .map(
          (label) =>
            `  ${label.name}: "${label.text}" needed ${label.neededWidth}x${label.neededHeight} ` +
            `(drawn ${label.renderedWidth}x${label.renderedHeight}) in ${label.rectWidth}x${label.rectHeight}` +
            (label.missingGlyphs ? ` missing [${label.missingGlyphs}]` : ""),
        )
        .join("\n"),
  ).toEqual([]);

  const thai = (report.labels ?? []).filter((label) => /[\u0E00-\u0E7F]/.test(label.text));
  expect(thai.length, `${scene} drew no Thai text at all`).toBeGreaterThan(0);
  expect(report.missingGlyphCount, `${scene} has labels with missing glyphs`).toBe(0);

  console.log(
    `${scene}: ${report.labelCount} labels fit, ${thai.length} of them Thai, 0 missing glyphs`,
  );

  return report;
}
