# Stage 09n — a shelf offer explains itself before you rent it

**Scope:** a read model in Domain/Application + Presentation + localization. No rent rule, price,
synergy rule or `SynergyEvaluator` behaviour changed; every number still comes from `data/` through
Core.

## 1. Summary

- A shelf offer is now explained by **one** new read path: `RunSession.ExplainOffer(slot)` →
  `LoadoutResolver.TryExplainOffer` (the same core a tier-1 copy in the chest resolves through) +
  `PotentialCombos` (a listing read off `synergy`/`item`, not an evaluation of a board that does not
  exist yet).
- The rent verdict is **not** a second rule: `Purchase` and the tooltip both ask the new
  `RunSession.CheckPurchase(slot)`, so "เช่าได้ / เบี้ยไม่พอ / หีบเต็ม" is literally the code the
  command would return.
- `CampPlayerView` now reports the shelf under the pointer through the **same** hit test the click
  uses (`CampWorldView.ShelfAtScreenPoint`), and the shelf the player walks up to publishes the same
  readout on its own board.
- The existing tooltip panel gained one line (ราคา + verdict); the combos line carries "ร่วมคอมโบกับ".
  Suppressed while the จัดย่าม panel is open, while the upgrade chooser is open, while a piece is in
  the air, and over a station's pin control or any UI.
- Seven localization rows (th/en), all ≥ 22 px at 1080p because they use the tooltip's existing font
  size.

## 2. Acceptance

| Item | Verdict | Evidence |
| --- | --- | --- |
| A. Survey (hit path, `ExplainPiece`, `TryExplain` by def id) before changing anything | **PASS** | `docs/stages/09n-report.md` §4; the chest already resolved a tier-1 piece with no combo, which is the path the offer now reuses |
| B. `ExplainOffer` read model (resolved numbers, combos listing, bag slots/weight, rent verdict from the same predicate as `Purchase`, no readout for a rented/empty shelf) | **PASS** | dotnet `OfferReadoutTests` 7/7 (every `shop_pool` row's offer numbers equal the chest copy's; verdict equals the command's code and a refusal changes no state; combos equal the table; empty/rented/unknown slots fail) |
| C. Presentation (hover → presenter, tooltip lines, walk-up detail, suppression, repaint after rent/re-roll, no per-frame allocation) | **PASS** | EditMode 6 new cases; PlayMode `CampShelfTooltipPlayModeTests` 5/5 including `Moving within one shelf allocates nothing` (measured 0 bytes) |
| D. Localization th/en + `gen.ps1` + `verify-generated` + `gen-wiki` | **PASS** | 7 new `camp.tooltip.offer.*` rows; `configHash AD069B3FC4A271F5…`; `verify-generated` 8 PASS / 1 WARN (the regeneration itself) / 0 FAIL; `llmwiki/content/` regenerated |
| Tests: dotnet / EditMode / PlayMode | **PASS** | dotnet **598** (591 + 7), EditMode **178/178** (172 + 6), PlayMode camp classes **52/52** (drag 25, world 9, synergy 9, chooser 4, shelf tooltip 5) |
| `UiLayoutCheck` at 1920×1080 and 1280×720 | **PASS** | `artifacts/ui/ui-report.md` — **PASS**, 0 failures, 0 overlaps, both sizes |
| Evidence images (weapon, charm, bag, "เบี้ยไม่พอ") at 1920×1080 from the real Game View | **PASS** | `artifacts/ui/stage09n-shelf-weapon-1920x1080.png`, `-charm-`, `-bag-`, `-insufficient-`; the drawn strings are in `artifacts/ui/ui-report-shelf-offer.md` |
| WebGL build + Playwright | **NOT RUN** | not a gate for this stage; no Web build was required (AGENTS "งาน UI") |

## 3. Not tested / risks

- **WebGL build and Playwright were not run.** The tooltip is Canvas UI and was verified in the
  Editor's Game View and by PlayMode tests; a browser pass is only scheduled at stages 10/12/14/15.
- The walk-up detail (a shelf the player stands at) is asserted in EditMode and drawn on the shelf's
  own world label; its *legibility* at 1280×720 is not measured — the world labels are a separate
  size rule (recorded since 09d, still open).
- The tooltip is 460×530 now (was 460×500). It fits both sizes (`UiLayoutCheck` and the PlayMode
  edge case), but a fourth synergy rule on one piece would push the combos block past its 150 px box;
  no shipped item has more than two.
- PlayMode allocation is measured per-thread in the Editor, not on WebGL.
- The bulk `--filter Camp` PlayMode run still trips the documented ambient Input System log asserts;
  every class is green on its own (recorded since 09l).

## 4. Commands actually run

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/gen.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-generated.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/gen-wiki.ps1
dotnet test "lib/Tests~/Core.Tests/ProjectSurvive.Core.Tests.csproj"
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Fast
```

Editor: `ProjectSurvive/Stage 09/5. Create Camp scene` (the tooltip panel gained its ราคา line),
`Tools/1. Capture UI` (layout check, both sizes), `Tools/7. Capture shelf offer tooltips in Play Mode
(Camp)` (the four evidence images). Unity Test Runner: EditMode (all), PlayMode
`CampShelfTooltipPlayModeTests`, `CampWorldPlayModeTests`, `CampBagDragPlayModeTests`,
`CampMergeChooserPlayModeTests`, `CampSynergyLinePlayModeTests`.

## 5. Where the work lives

- Domain: `lib/Runtime/Domain/Inventory/OfferReadout.cs` (new), `LoadoutResolver.TryExplainOffer` /
  `PotentialCombos` and the `Explain` core both paths share, `lib/Runtime/Domain/Shop/OfferCheck.cs`.
- Application: `RunSession.CheckPurchase` (now the one rent predicate; `Purchase` calls it) and
  `RunSession.ExplainOffer`.
- Presentation: `CampPresenter.HoverOffer` / `FocusOffer` / `PublishOfferTooltip` /
  `PublishFocusedShelf`, `CampPlayerView.UpdateHover`, `CampWorldView` (focused board),
  `CampUiState.TooltipPrice` / `FocusedShelfDetail`, `CampTooltipView` (one more line).
- Data: seven `camp.tooltip.offer.*` rows in `data/localization/localization.csv`.
