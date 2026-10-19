# Stage 09h — the tooltip's numbers follow the upgrade

## 1. Summary

The camp tooltip was reading the `skill` table row directly, so a piece at **3/3** still printed its
1/1 damage and cooldown, and a picked upgrade variant left no trace on screen. The tooltip now reads
one **Application read model** — `RunSession.ExplainPiece` → `LoadoutResolver.TryExplain` →
`PieceReadout` — that copies the resolved numbers straight out of the `CombatLoadoutSnapshot` the
next floor receives, so the screen cannot disagree with the battle. The panel grew one line for the
chosen upgrade steps; the upgrade chooser's left column is built by the same producer, so both show
the same six lines. The Editor capture, which had been writing **all-black** shots through
`ScreenCapture`, now renders the frame through the scene camera into a 1920×1080 target, and its
before/after shots are a real before/after (pixel proof 5/5).

## 2. Diagnosis (item A)

ยันต์เพลิงกาลี at **3/3** in the shipped tables:

| Source | Damage | Cooldown | Focus | Where it came from |
| --- | --- | --- | --- | --- |
| The old tooltip | 10 | 0.9 | 4 | the `skill` row, read by the presenter |
| `WeaponLoadout` (resolved) | 15 | **0.765** | 4 | tier 2 `คาถาไว` ×0.85, tier 3 `คมอาคมชั้นสูง` +35 % damage |
| `constant.min_cooldown_seconds` | — | **0.2** | — | the floor |

The floor was **not** the reason 0.9 looked unmoved: 0.765 is far above 0.2, so the number on screen
was the base row, not a clamp. The first read-model build then printed **0.8**, because the
localization row formatted the cooldown with `{1:0.#}` — one decimal cannot show a 15 % step from
0.9. The row is now `{1:0.##}` and prints **0.77**. That is a display-format change only; no
balance value in `data/` moved.

## 3. What changed

- **Domain** — `PieceReadout` (+ `PieceStatValues`, `PieceUpgradeStep`) and
  `LoadoutResolver.TryExplain`: base row, resolved row, the steps' variant ids and modifier rows,
  the combos acting on the piece, and `CooldownAtMinimum` decided from the value **before** the clamp.
  A board piece is quoted from the resolver's own snapshot; a chest piece is resolved with the same
  `UpgradeAccumulator` and cooldown helper but with no combos (a chest piece neither attacks nor
  receives a bonus).
- **Application** — `RunSession.ExplainPiece(piece)` is the Presenter's only door to those numbers.
  Read-only; the snapshot stays cached per bag revision.
- **Presentation** — `CampPresenter` formats the read model and nothing else: สถานะ (resolved row,
  then `ค่าพื้นฐาน …`), รุ่นย่อยที่เลือก (one line per confirmed variant with the card's own change
  wording), คอมโบ (totals and rule names), and a floor note when the minimum decided the number.
  `CampUiState.TooltipUpgrades` + `CampTooltipView._upgradesLabel` + the rebuilt panel
  (460×500 in `CampSceneBuilder`). The chooser's left column adds the same 6th line and lays the
  wrapped lines out by their own heights.
- **`data/`** — three new localization rows (`camp.tooltip.base.format`, `.minimum.format`,
  `.upgrade.format`) and the cooldown precision above. No balance number, no loadout rule, no
  `skill`/`upgrade` table change.
- **No Core bug found (item E)** — `LoadoutResolverTests` and `PieceReadoutTests` pin the readout
  against the snapshot entry field for field, including the variant that is applied and the undo.

## 4. Acceptance

| # | Item | Verdict |
| --- | --- | --- |
| A | Diagnosis printed for the same piece: tooltip value, resolved `WeaponLoadout`, `min_cooldown_seconds` | **PASS** — §2; captured notes print all three (`resolved after the upgrade: … cooldown 0.765 (base 0.9) … min_cooldown 0.2 (at minimum: False)`) |
| B | Application read model of the resolved piece (damage, cooldown, MP, tier, combos, chosen variants + changed lines) from the resolver's source | **PASS** — `PieceReadout` / `LoadoutResolver.TryExplain` / `RunSession.ExplainPiece`; the presenter has no arithmetic |
| C | Tooltip: name, tier, resolved with base, variant names + per-step result, combos; floor note | **PASS** — captured labels: `ดาเมจ 15 · คูลดาวน์ 0.77 วิ · ใช้สมาธิ 4` / `ค่าพื้นฐาน ดาเมจ 10 · คูลดาวน์ 0.9 วิ · ใช้สมาธิ 4`; `ระดับ 2 · คาถาไว: คูลดาวน์ -15%` and `ระดับ 3 · คมอาคมชั้นสูง: ดาเมจ +35%`; combo line. The floor branch is a separate test (`A_cooldown_held_up_by_the_floor_is_reported_as_at_its_minimum`) |
| D | The chooser's left panel uses the same producer | **PASS** — `CollectSourceLines` is the single producer for both; `CampBagScreenView` grew the 6th label |
| E | If a variant had no effect in the real snapshot, report it as a Core bug instead of hiding it | **PASS** — no such case: the readout equals the snapshot and the −15 % moves 0.9 → 0.765 (tested); nothing was hidden in the tooltip |
| Tests | After `ConfirmMerge` with variant X the tooltip equals the snapshot and differs by X's modifier; no-variant piece shows the new tier with no step line; undo restores | **PASS** — `PieceReadoutTests` 8 (xUnit), `CampPresenterTests` (×2) and the chooser suite; `dotnet test` **519**, EditMode **156/156** |
| Images | Editor capture of the tooltip before and after at 1920×1080 | **PASS** — `artifacts/ui/stage09h-tooltip-before-1920x1080.png` / `-after-…`, report **PASS**, 0 failures, 5/5 sample pixels changed when the panel was hidden. Inspected in Play Mode as well: the shot shows ระดับ 3/3, 15 / 0.77 / 4 over 10 / 0.9 / 4, both variant lines and the combo line, legible and inside the 460×500 panel |

## 5. Not tested, and risks

- **WebGL build and Playwright: NOT RUN.** 09h is not a web gate and the Editor held the project
  lock; no browser was opened.
- **The bulk PlayMode run is red on the documented ambient-log flake**: **22/48** passed, the 26
  non-passes carrying 12 `IndexOutOfRangeException` and 9 `Cached unprocessed value` log assertions
  plus the cascades they cause. The two fixtures this stage can affect pass **on their own**:
  `CampBagDragPlayModeTests` **12/12** (including the tooltip's draw order) and
  `CampMergeChooserPlayModeTests` **4/4**.
- The capture's render path is the tool's own camera-into-RenderTexture pass, the same one the rest of
  the tool uses. It is not `ScreenCapture`, which is what made the earlier shots black; if a future
  tooltip feature depends on overlay-only behaviour, the capture would not reproduce it.
- **No ADR.** The read model is a second *entry point* to `LoadoutResolver`, not a second rule source,
  and the alternative the brief rejects (the presenter reading a `skill` row) is the bug this stage
  exists to remove, so there was no genuinely different design that another stage would inherit.
- Unrelated working-tree changes were left untouched and **not** committed: `.agents/mcp.json`
  (two MCP servers switched off — pre-existing) and the TMP `LiberationSans SDF - Fallback` asset
  (a Unity 6 re-serialisation, `serializedVersion` 6 → 8).
- The two-decimal cooldown applies to every tooltip stat line. Values that end in a tenth still read
  `0.9`; nothing else was reformatted.

## 6. Commands run

```pwsh
python … # edit data/localization/localization.csv (3 rows + {1:0.#} -> {1:0.##})
pwsh -NoProfile -File tools/gen.ps1                    # config hash 00489C97…, RESULT: OK
pwsh -NoProfile -File tools/test.ps1 -Suite Fast       # PASS 10 | FAIL 0 | NOT RUN 3 | WARN 0, 107.9 s
#   dotnet 519, Unity EditMode 156/156, data gates 21 fixtures
pwsh -NoProfile -File tools/test.ps1 -Suite Full       # PASS 10 | FAIL 1 | NOT RUN 2, 206.1 s
#   the FAIL is the bulk PlayMode flake above; web steps NOT RUN (Editor lock)
unity command run_tests --mode playmode --filter_type testName --filter CampBagDragPlayModeTests   # 12/12
unity command run_tests --mode playmode --filter_type testName --filter CampMergeChooserPlayModeTests  # 4/4
# Editor: ProjectSurvive/Tools/4. Capture the tooltip before and after an upgrade (Play Mode)
```

## 7. Evidence

- `artifacts/ui/ui-report-tooltip.md` / `.json` — **PASS**, 0 failures, 1920×1080, tooltip
  `(1191.887, 8) 460×500`, 5/5 painted; the notes carry the six label strings before and after and
  the resolved values from the run.
- `artifacts/ui/stage09h-tooltip-before-1920x1080.png`, `…-after-1920x1080.png`.
- `artifacts/stage09h/fast.log`, `artifacts/stage09h/full.log`,
  `artifacts/stage09h/playmode-camp.json`, `artifacts/stage09h/playmode-chooser.json`.
- In Play Mode I also looked at the screen itself after the same two merges (`capture_game_view`,
  screen source): the numbers, the two variant lines and the combo line are legible in the
  460-pixel panel.
