# ADR 0008 — Thai text: uGUI + TextMeshPro for player UI, static baked font assets

- **Status:** Accepted
- **Date:** 2026-10-14
- **Inherited by:** template-v1 — the decision still applies; the numbering below is the template's own (0001–0099).
- **Stage:** 03 (Unity shell, first Web build, Thai text spike)
- **Related:** [ADR 0002](0002-thin-mvp-ui-approach.md) (thin MVP UI),
  [ADR 0003](0003-performance-baseline-machine-and-browser.md) (measured on a named
  browser), [`llmwiki/systems/unity-shell.md`](../../llmwiki/systems/unity-shell.md),
  [`llmwiki/gotchas/tmp-font-assets-in-builds.md`](../../llmwiki/gotchas/tmp-font-assets-in-builds.md)

## Context

Every player-facing string is Thai (AGENTS rule 5), and the target is a desktop
browser running a single-threaded WebGL2 player. Thai needs pre-positioned upper
vowels, tone marks, below-vowels and descenders; a font asset that misses one code
point turns player text into replacement boxes, and a *dynamic* font asset hides
that fact until the moment it happens on a player's machine.

The GDD does not name a UI stack, and the project has two available: **uGUI +
TextMeshPro** (`com.unity.ugui`, ships with 6000.3) and **UI Toolkit / TextCore**
(`com.unity.modules.uielements`). ADR 0002 fixes the *architecture* (thin MVP, no
logic in Views) but not the renderer.

Stage 03 therefore had to answer two questions with numbers rather than preference:

1. Can we ship a Thai font asset that provably draws every string we ship?
2. If both stacks can, which does the player UI use?

## What was measured

Same font file (`Assets/Main/Fonts/Sarabun-Regular.ttf`, SIL OFL 1.1 — see
`Fonts/OFL.txt`), same three strings from `tblocalization`, both stacks at the **same
nominal size (48)**, same machine, same build. Glyph coverage is asked per character
with `HasCharacter` against the *shipped* asset, not with `TryAddCharacters`
(see the gotcha note on why its return value cannot be trusted).

| Stack | String (key) | Editor | WebGL player (Chrome 154) | Missing |
| --- | --- | --- | --- | --- |
| uGUI + TMP | `ui.title` | 289.7 × 62.4 | 289.69 × 62.41 | 0 |
| uGUI + TMP | `ui.spike.thai` | 829.7 × 62.4 | 829.67 × 62.41 | 0 |
| uGUI + TMP | `ui.spike.thai.marks` | 704.8 × 62.4 | 704.79 × 62.41 | 0 |
| UI Toolkit | `ui.title` | 290 × 63 | 290 × 63 | 0 |
| UI Toolkit | `ui.spike.thai` | 830 × 63 | 830 × 63 | 0 |
| UI Toolkit | `ui.spike.thai.marks` | 705 × 63 | 705 × 63 | 0 |

`sizes in px; "marks" = tone marks, upper vowels, below vowels and the rare
consonants ฎ ฏ ฐ ฅ ฃ ฤ ๅ ษ ญ`

Verdicts published by the spike (`__projectSurviveThaiSpike`): Editor —
`coversEveryGlyph=true`, `stacksMeasureEqually=true`; WebGL —
`coversEveryGlyph=true`, `stacksMeasureEqually=true`, `rowCount=6`,
`totalMissingGlyphs=0`. The two stacks agree to within 1 % at the same nominal size,
so **the text engines do not disagree about Thai**; what differs is what the assets
and the build pipeline do to the glyph data.

Baked character set: printable ASCII plus the **assigned** Thai code points
(`U+0E01..U+0E3A`, `U+0E3F..U+0E5B`) = **182 characters**, verified by reading the
character tables back out of both `.asset` files (182 entries, 87 of them Thai).

## Decision

### 1. Player-facing UI is uGUI + TextMeshPro

Title (and every later HUD/menu screen) renders through TMP with our own
`TMP_FontAsset`. Reasons, in order:

- **Coverage is a property of the asset we ship**, and it is asserted in the player
  (Playwright) rather than inferred from the Editor. That is the property this stage
  needed and the one that failed twice before it was right.
- **One asset pipeline for text we must control**: TMP's font asset, its atlas and
  its material are ours and are versioned with the project.
- **UI Toolkit adds a second font-asset type and a `PanelSettings` asset per scene**
  for no measured benefit at this stage. Its TextCore atlas is currently kept only
  because the spike compares the two stacks (see below).

The thin-MVP split of ADR 0002 is unchanged: `TitlePresenter` decides the strings,
`TitleView` assigns them, `UiState` carries them.

### 2. Font assets are baked **Static**, never Dynamic

`ProjectSetup.EnsureFontAssets` creates both atlases, bakes the required set, then
freezes them to `AtlasPopulationMode.Static`:

```
Dynamic -> ReadFontAssetDefinition -> ClearFontAssetData -> TryAddCharacters
        -> verify (characterTable.Count + HasCharacters) -> Static
```

Why Static, with the evidence:

- TMP's **pre-build processor clears the character and glyph tables of every Dynamic
  asset with `clearDynamicDataOnBuild`** — on the *project asset*, at build time, with
  no restore. The first Stage 03 Web build did exactly that: the Editor reported
  `missingGlyphs=0` while the shipped player could look up only one Thai character and
  the `.asset` on disk had `m_GlyphTable: []`, `m_CharacterTable: []`. A Static asset
  is excluded by that processor's condition.
- A Dynamic asset defers coverage to runtime ("rasterise on first display"), which
  makes coverage *unprovable*: in a player, `HasCharacter` answers "is this glyph
  already rasterised?", not "can the shipped font draw it".
- Static removes per-glyph rasterisation from the player's frame time — a real
  consideration on a single-threaded WebGL2 target.

Cost: adding a script or a code point means extending
`ProjectSetup.BuildBakedCharacterSet()` and re-running
`ProjectSurvive > Stage 03 > 1. Configure project`; the bake then re-verifies itself and
**throws** if either atlas cannot draw the set.

### 3. The bake verifies itself, and the browser repeats the check

The bake reads the tables back and fails loudly on a short set (a silently empty font
asset reached a browser build once). After that, two independent gates repeat the
coverage question on the artifact that ships:

- PlayMode: `Thai_spike_scene_measures_both_stacks_without_missing_glyphs` loads the
  spike scene, requires one row per stack per string, and requires
  `TotalMissingGlyphs == 0`.
- Playwright: `renders the Thai text spike with full glyph coverage in the browser`
  asserts `totalMissingGlyphs === 0` and `coversEveryGlyph === true` on the **WebGL
  player's own report**, in the real target browser.

### 4. The UI Toolkit path stays as a harness, not as player UI

`Assets/Main/Scenes/ThaiTextSpike.unity` keeps both stacks and is still built, because
it is the only place the two engines can be compared under the real target. It is the
reason `SceneBoot` accepts `?scene=<allow-listed name>`: a page has no other way to
reach a diagnostic scene. When the comparison is no longer needed, delete the scene,
`ThaiSpikeToolkitView` and the TextCore atlas — that is ~2.2 MB of build the project
does not otherwise need.

## Consequences

- Player text has one owner: the localization table → `LocalizationService` →
  presenter → TMP label. No screen may use UI Toolkit for player-facing text without
  superseding this ADR.
- **Glyph coverage becomes a build-time responsibility.** A string with a code point
  outside the baked set renders as a missing-glyph box in the player and is caught by
  the spike test only for the strings the spike measures — the set, not the test, is
  the guarantee (ASCII + all assigned Thai code points covers every Thai string).
- The two font atlases add ~4.4 MB to the Development build (measured: the build grew
  from 116.85 MB to 117.50 MB with the baked tables, having earlier *lost* 650 KB by
  shipping empty ones).
- The spike's numbers are **layout and coverage** measurements. They are not frame
  time, and no timing claim is made here (ADR 0003 governs any future timing claim,
  and needs a Release build).
- If a later stage needs text the baked set cannot draw (a new script, emoji, XML
  markup), the answer is to extend the set and re-bake — not to switch an asset back
  to Dynamic.
