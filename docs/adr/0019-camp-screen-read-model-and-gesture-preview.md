# ADR 0019 — The camp screen's read model, its drag preview, and why the report freezes

- Status: accepted
- Date: 2026-10-15
- Stage: 09 (with 09b and 09c)

## Context

Stage 09 puts the first *editable* screen in front of the player: a board, a chest, an altar, four
shelves and a group of buttons, all of which change the run through the Stage 05/06 command
surface. Three questions had no answer in the existing code, and each of them had a tempting wrong
answer that a player would eventually see.

1. **What a drag shows before it is dropped.** "Preview วางได้และวางไม่ได้ โดยไม่แก้ Domain จนผู้เล่น
   ยืนยัน" can be implemented by asking the run, or by letting the view decide from the piece and the
   cell it is over. The second is how a preview and a real drop start to disagree.
2. **How often the screen publishes its state.** `CampReport` writes a JSON document onto `window`
   for the browser tests to read. A drag changes something on every pointer move.
3. **Where the shop lives.** The GDD's Stage 09 asks for a stall with a price tag; Stage 09c asks
   for the same purchase, reached by walking. Two affordances for one command is how a player ends
   up looking at a slot that is already rented (see [ADR 0016](0016-run-settlement-and-shop-commit-boundaries.md)).

## Decision

### 1. The preview is a dry run of the command, not a second rule

`RunSession.PreviewDrop(piece, zone, origin, rotation)` answers what a release *would* do by asking
the same `BackpackEditor` predicates the accepted command asks — `CanPlace`, `CanMerge`,
`CanTransferToChest`, `CanRecycle` — and it writes nothing: no revision moves, no event is emitted,
nothing is recorded in the replay log. The only thing it decides on its own is **intent** (which
command a zone and the piece under the pointer imply), which is one place rather than one `switch`
per view.

Consequences:

- A green preview and an accepted command are the same condition read twice, so they cannot drift.
- A refused drop still reports the *rule's own* error code (`inventory.item_stranded`,
  `inventory.locked`, …), because the code comes from the predicate rather than from the view.
- The view may call it on every pointer move; it allocates nothing and changes nothing.
- A preview is not a lock. The drop re-asks the run (`Drop()` calls `PreviewDrop` again and then the
  command), so a board that moved under the pointer cannot be acted on with a stale verdict.

### 2. The state document is frozen while a gesture is live

`CampSceneDirector.Publish` publishes the layout first and then returns early when
`Presenter.IsDragging`. The reason is allocation, not politeness: the state document is a
hand-rolled JSON string, and rebuilding it per pointer move would put a string concat in the one
path the GDD calls a hot path. The layout document keeps publishing (a shelf's on-screen position
must stay true while the camera follows the walker), so a gesture still moves the picture.

Consequences:

- A test *cannot* read `dragging: true` from the page: the document it would read it from is the one
  that is not being written. The camp's browser and PlayMode tests therefore assert the **release**
  (`dragPreviews` for the pick-up, `dragCancels` / `placed` / `invalidDrops` for the outcome).
  This was learned by writing the opposite assertion first and watching it time out.
- The counters are part of the document, so a gesture's *result* is visible within one publish
  interval (four a second), and never the gesture's interior.

### 3. The shop is a place, and the UI list is gone

From Stage 09c the camp has one shelf per shop slot, standing in the sala, each drawing its own
piece, price, rented/empty state and lock pin in world space. The Stage 09 panel-era list of shop
cards does **not** also exist: there is one affordance per slot, and the four `rent*`/`pin*` controls
still published in the layout report are the shelf's own click targets, not a second list.

The additive UI buttons the Stage 09 prompt asks for (`ออกเดินทาง`, `คืนสภาพ`, `ล็อกย่าม`,
`จัดย่าม`) remain, because they are the keyboard/mobile path and the path the browser tests drive;
walking and pressing `E` reaches the *same* `RunSession` command through the same presenter method,
so the two paths cannot disagree about a rule.

## Evidence

- The preview/command agreement is asserted directly: `CampPresenterTests.Buying_placing_rotating_
  merging_giving_back_and_restoring_reach_the_same_state_as_the_same_commands_issued_directly`, and
  `lib/Tests~/Core.Tests/Run/BagDropPreviewTests.cs` over the real tables.
- The freeze is why `tests/web/camp.spec.ts` step 9 polls `dragCancels` rather than `dragging`
  ([09-report](../stages/09-report.md) §5 records the failing run that taught it).
- One affordance per slot: `CampWorldPlayModeTests.Clicking_a_shelf_rents_it_without_walking` and
  `Clicking_a_shelfs_lock_pins_it_against_the_reset` drive the shelf itself; the browser test rents
  the shelf it walked to.
- The `DragCancels` assertion is in `CampPresenterTests.Escape_cancels_the_drag_and_the_picture_
  comes_back` (EditMode) and in the browser flow test, so the decision is measured on both sides of
  the build.
