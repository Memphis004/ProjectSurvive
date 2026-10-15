# Stage 09d — ความคมชัดของตัวอักษร (Thai text sharpness)

Measurement pass over Stage 09's Thai text. No rule, `data/` or scene behaviour changed except the
two bag labels raised to the 22 px floor (§2). Every number below is measured, and both new tools
measure ink twice (engine layout box + painted pixels) and agree to the pixel.

Tools: `tools/font-metrics.py` (font file), `tools/screen-text-metrics.py` (screenshot, cropping the
rectangles the **player itself** publishes), `tests/web/scripts/collect-geometry.mjs` (those
rectangles for the frame that was shot).

## 1. The font actually used, per canvas — with its bake settings

Every canvas uses **one** asset, read from the loaded objects: `Assets/Main/Fonts/Sarabun-Regular SDF.asset`
(Title, Battle, Camp and ThaiTextSpike all reference it; runtime labels take it through the serialized
`_font` of `CampBagScreenView` / `CampWorldView` / `BattleViewSettings`).

| Setting | `Sarabun-Regular SDF` (in use) | `THSarabunPSK SDF` |
| --- | --- | --- |
| Family / style | Sarabun / Regular | TH SarabunPSK / Bold |
| **Sampling point size** | **90** | **227** |
| **Padding** | **9** | **9** |
| **Render mode** | **SDFAA** (`4165`) | **SDFAA** (`4165`) |
| Population mode | Static (frozen bake) | Dynamic |
| Atlas | 1024², 1 texture, multi-atlas on | 1024², multi-atlas on |
| Characters baked | 182 | **0** |
| Size on disk | 2,197,914 B | 8,608,203 B |

`4165` is `GlyphRenderMode.SDFAA`, not a guess: `TMP_FontAsset.CreateFontAsset(Font)` is
`(font, 90, 9, SDFAA, 1024, 1024)` in the installed package (`com.unity.ugui@bb329a87fcdc`,
`Runtime/TMP/TMP_FontAsset.cs:538`).

**THSarabunPSK is not used at all** — no scene, prefab or field references its GUID
(`67193ea371f493946b9934be4187de73`), and its bake holds zero characters. It is still the **largest
asset in the Web build**: 4,220,916 B packed into `resources.assets`
(`artifacts/stage09/build-report.json → largestAssets[0]`), because `Assets/Resources/` is always
shipped. Deleting it is a one-file, 4 MB change — recommended, **not** done here (§5).

## 2. Canvas Scaler and the 22 px floor

All four canvases: `Screen Space - Overlay`, **`Scale With Screen Size`, reference 1920 × 1080,
`MatchWidthOrHeight 0.5`**, PPU 100 — exactly the item's setting. Scene labels: Title 2 (min 28 px),
Battle 18 (min **22**), Camp 39 (min **22**); the runtime labels (bag pieces, drag ghost) are 22 px,
and `CampBagDragPlayModeTests.The_panel_layout_has_no_overlapping_regions_and_no_tiny_labels` asserts
every panel `TMP_Text` against a 22 px floor.

Measured in the real Web build, on the จัดย่าม button (`screen-text-metrics.py`, region `openBag`):

| Canvas | Device pixels | Ink runs | Ink, tone-mark top → baseline |
| --- | --- | --- | --- |
| 1920 × 1080, dsf 1 | 1920 × 1080 | 6 + 14 px | **21 px** |
| 1280 × 720, dsf 1 | 1280 × 720 | 4 + 9 px | **14 px** |
| 1280 × 720, dsf 2 | 2560 × 1440 | 8 + 18 px | **27 px** |

21 px of ink for a 22 px font is the font's own figure (§3), so at the 1080p base the scaler is 1.0 and
the text is drawn at the size the scene says. At 1280 × 720 the log-average factor is
`2^(0.5·log2(1280/1920)+0.5·log2(720/1080)) = 0.667` — measured 14 against a predicted 14.7 — so
**the 22 px floor is a 1080p-base statement**: on a 720p window every canvas label is two-thirds of
it. Options, none taken alone here: accept (documented), raise the base to 24–26 px, or match on
height. At deviceScaleFactor 2 Unity draws the same CSS layout into twice the pixels (3840 × 2160 for
1920 × 1080), which is why a HiDPI screen looks sharper.

**Not covered by this rule — the world-space labels.** Shelf prices, lock labels, ยายจันทร์'s sign and
the damage numbers are 3D `TextMeshPro`, sized by camera distance. The lock control's published rect
measures **29.5 × 11.8 px with 12 px of ink** at 1080p (20 × 8 / 7 px at 720p, 39 × 16 / 16 px at
dsf 2) — about half the floor, a third of it at 720p. Reported, not changed: it is a different rule
and the fix is a design decision. The price labels' rects are unpublished, so they are unmeasured.

## 3. THSarabunPSK at 22 px: 31 % shorter — and the project already left it behind

`tools/font-metrics.py --size 22` (`artifacts/stage09d/fonts/font-metrics.json`):

| At 22 px | Body (`ก`) | Ascender letter (`ป`) | Sample with tone marks | Latin x-height | Sample advance |
| --- | --- | --- | --- | --- | --- |
| Sarabun-Regular | **13 px** (0.591 em) | 18 px | 22 px | 11 px | 309 px |
| THSarabunPSK | **9 px** (0.409 em) | 12 px | 19 px | 7 px | 220 px |

THSarabunPSK needs **31 px** (1.41 ×) for parity. Comparison images:
`artifacts/stage09d/fonts/thai-22px-dark.png`, `thai-22px-light.png`, `thai-22px-scaling.png` (Sarabun
22 px beside THSarabunPSK grown to 31 px). Licence check: the font in use is **Sarabun by Cadson
Demak, SIL OFL 1.1**, licence text in-repo (`Assets/Main/Fonts/OFL.txt`) — and it is *not* TH Sarabun
PSK or TH Sarabun New, which is the confusion behind the item's premise; TH Sarabun PSK is the
Thai-government/SIPA face under a Thai-language font licence, not OFL, which is a second reason not
to ship it. Licensed OFL 1.1 alternatives if a third look is ever wanted: **Noto Sans Thai** /
**Noto Sans Thai Looped** (looped = traditional Thai letterforms), **IBM Plex Sans Thai**, and Cadson
Demak's **Prompt** / **Kanit** — each to be judged with `font-metrics.py` before any bake, since the
body-to-em ratio is what decides legibility at 22 px.

## 4. Screenshots of the real Web build

`tests/web/screens.spec.ts` (opt-in: `SCREENS=1 npx playwright test screens.spec.ts`; not a gate —
Playwright gates are Stages 10/12/14/15). **13 / 13 passed in 2.4 min**, 17 files in
`artifacts/stage09d/screens/`: camp ×4, จัดย่าม open ×4, the template's own 960 × 600 canvas ×1,
title ×4, battle ×4 — each at 1920 × 1080 and 1280 × 720 with `deviceScaleFactor` 1 and 2. The
run's own log, which is the proof the sizes mean something:

```
camp 1280x720-dsf2: the player adopted 2560x1440 device pixels for a 1280x720 CSS canvas (deviceScaleFactor 2)
camp 1920x1080-dsf2: the player adopted 3840x2160 device pixels for a 1920x1080 CSS canvas (deviceScaleFactor 2)
camp default: a 1920x1080 viewport, a canvas of 960x600 CSS pixels, the player drawing 960x600 pixels
```

The **canvas element** is resized, not the window (the template pins it at 960 × 600 CSS — the
`viewport-template-canvas` shot is the one that leaves it as shipped), and images are captured at
Playwright's `scale: "device"`, so a dsf 2 file is the framebuffer the display gets.

## 5. Result, and what is not claimed

| # | Item | Verdict |
| --- | --- | --- |
| 1 | Font per canvas + point size / padding / render mode | **PASS** — §1 |
| 2 | Scaler 1920×1080; Thai ≥ 22 px at the 1080p base | **PASS** — §2, **21 px of ink measured on screen** at 1080p; floor test 1/1; the 720p (×0.667) and world-space (12 px) consequences stated |
| 3 | Other Thai font if THSarabunPSK is too small, licence checked, with a comparison image | **PASS** — §3 (31 % smaller at 22 px; Sarabun OFL already in place; 3 images; 4 licensed alternatives) |
| 4 | Screenshots at 1920×1080 / 1280×720 × dsf 1 / 2 | **PASS** — §4 (13/13, 17 PNGs) |

**Commands run:** the three tools above; `SCREENS=1 npx playwright test screens.spec.ts` (13 passed);
Unity Test Runner EditMode **57 passed / 0 failed**; PlayMode filtered to the floor test **1 passed**;
PlayMode whole suite **10 passed / 10 failed** — see the blocker.

**Files:** `tools/font-metrics.py`, `tools/screen-text-metrics.py`,
`tests/web/scripts/collect-geometry.mjs`, `tests/web/screens.spec.ts` (new);
`CampBagScreenView.cs` (two labels 14/15 → 22 px), `ProjectSetup.cs` (`UiFontSize = 22f` for the
authored panels), `CampBagDragPlayModeTests.cs` (the floor assertion), `knowledge.md`,
`llmwiki/stage-log.md`, this report. Artifacts: `artifacts/stage09d/` (ignored by git, kept on disk).

**Blocker (unsolved, not caused by this stage).** In a freshly launched Editor the whole PlayMode suite
is **10/20**: seven failures are `IndexOutOfRangeException` from `InputSystem.InputManagerStateMonitors.
AddStateChangeMonitor` via `InputSystemUIInputModule.OnEnable`, three are `[Error] Cached unprocessed
value unexpectedly became outdated` from `NativeInputRuntime` — all `LogAssert` failures on ambient
messages, **none a failed assertion**, all in the Input System package. Three whole-suite runs with
different durations gave the same result, the owning test passes **1/1** alone, and Stage 09's session
reported all 24 green on this code. Also: `run_tests` can answer with a *previous* run's results
(compare per-test durations before quoting a number).

**Risks.** The world-space labels are below the floor (§2); the orphan font ships 4 MB for nothing
(§1); `TMP Settings.asset` still points its default font at LiberationSans with no Thai fallback, so a
label that forgets to assign Sarabun would draw blanks. Screenshots are Chrome 154.0.8037.98 only
(`tools/versions.lock.json`; Firefox is not installed here), and ADR 0003 limits rendering claims to a
named browser.
