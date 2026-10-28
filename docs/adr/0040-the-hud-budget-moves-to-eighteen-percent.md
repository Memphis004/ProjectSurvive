# ADR 0040 — The Battle HUD budget is 18 %, not 15 %

- **Status:** Accepted
- **Date:** 2026-10-28
- **Stage:** 08i
- **Related:** [ADR 0018](0018-battle-camera-projection.md) (the camera the HUD sits over),
  `docs/stages/08g-report.md` (the stage that removed the mini ย่าม to reach 15 %),
  `docs/stages/08i-report.md`, `UnityProject/Assets/Main/Scripts/Presentation/Battle/Hud/BattleHudArea.cs`,
  `UnityProject/Assets/Main/Tests/PlayMode/BattlePlayModeTests.cs`

## Context

Stage 08g-2 met its sixth item — "วัดพื้นที่ที่ UI บังจอ ต้อง ≤ 15 % ที่ 1920×1080" — by removing the mini
ย่าม and the cooldown plates from the Battle screen: five panels summing to 37.86 % could not be moved
into 15 %, so the stage deleted a panel and re-cut the other four. The shipped screen measured **13.95 %**
at 1920 × 1080 and at 1280 × 720, and a PlayMode test held it there.

Stage 08i was opened because that cost the player something the fight needs: **which item is ready and
which is on cooldown.** With no mini bag there is no surface to draw `IBattleFrame.Cooldowns` on, and a
survivor-like asks that question several times a second.

## Decision

**Restore the mini bag as a fifth panel, 288 × 216 at 1080p (3.00 % of the screen), and move the ceiling
to 18 %.** `BattleHudArea.Budget = 0.18f`, one named constant, asserted at both canvas sizes by
`BattlePlayModeTests.The_hud_covers_at_most_eighteen_percent_of_the_screen_at_both_sizes`.

Measured on the shipped scene (`artifacts/ui/ui-report.json`, Stage 08i): five disjoint panels —
floor block 380 × 72 (1.32 %), resources 513.6 × 216 (5.35 %), status frame 380 × 212 (3.89 %), timeline
1468 × 48 (3.40 %), mini ย่าม 288 × 216 (3.00 %) — sum **16.95 %** at 1920 × 1080 and 16.95 % at
1280 × 720.

## Why this and not the alternatives

- **Keep 15 % and shrink the four old panels by 3 points.** Rejected: 08g-2 had just re-cut those four
  to give the fight its screen back, and the user asked for that layout to stay ("คงตำแหน่งและขนาดของ
  4 พาเนลเดิมไว้ ไม่จัดใหม่"). Reclaiming 3 points would mean shrinking the floor block and the status
  frame until their text no longer fits the 22 px Thai floor the project has held since Stage 09d.
- **Keep 15 % and drop a different panel.** There is no other panel to drop: the floor block, the run's
  numbers, the status frame and the timeline are each read continuously. The bag was the only one that
  had ever been removable, and removing it is what this stage reverses.
- **Draw the bag and the cooldowns transiently, over the fight, only when something changes.** Rejected
  as a worse answer to the same question: a panel that appears for a second is exactly the thing a
  player cannot glance at mid-fight, and it would still occupy screen area at the moment it matters.
- **Keep the ledger at 15 % and call the bag "not a panel".** Rejected as dishonest bookkeeping: the
  bag is opaque enough to occlude what is behind it, so it belongs in the sum.

18 % is the smallest named ceiling that fits 16.95 % with room for the panel to grow one step (a future
bag of 4 × 4 cells at the same cell floor) without re-opening the argument. The ledger is still a test
over the shipped scene, still requires the panels to be disjoint before it will sum them, and still does
not count the dev menu (shut behind F12 until pressed).

## Consequences

- **Positive:** the player sees readiness and cooldown on every caster again, at both canvas sizes, and
  the four panels the user approved in 08g are byte-for-byte the same rectangles.
- **Positive:** the ceiling is a single constant, so a later stage that adds a sixth panel has one number
  to argue with rather than a number hidden in a test.
- **Negative:** the HUD may now cover up to 18 % of the screen. This is 3 points more than the 08g item
  asked for; the stage's brief accepted that trade explicitly, and the bag's own panel is the most
  transparent surface on the screen (0.72 vs the others' 0.92) so the arena still reads through it.
- **Negative:** the budget no longer says "≤ 15 %" anywhere, so a reader of the old report needs this
  ADR to know why the number moved.
- **Revisit if:** the bag becomes larger than 3 × 3 in `data/tables/bag.csv`, or a sixth panel is
  proposed — then the ceiling is the argument again, and the cell-size fit is the measurement.
- **Locked by tests:** `BattlePlayModeTests.The_hud_covers_at_most_eighteen_percent_of_the_screen_at_both_sizes`
  (both sizes, disjointness first) and `UiLayoutCheckTests` (no overlap with the bag, with the dev panel
  shut or open).
