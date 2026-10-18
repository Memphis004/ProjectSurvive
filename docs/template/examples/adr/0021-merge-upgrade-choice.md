# ADR 0021 — A merge offers an upgrade choice, and the offer is a pure function of the run

- **Status:** Accepted (Stage 09f, second half).
- **Date:** 2026-10-16
- **Related:** [ADR 0014](0014-backpack-editing-undo-and-placement-ownership.md) (commands, snapshot
  undo), [ADR 0019](0019-camp-screen-read-model-and-gesture-preview.md) (the drag preview as a read
  model), [ADR 0020](0020-bag-moves-carry-the-items-on-them.md) (the other half of Stage 09f),
  AGENTS rules 5 (numbers live in `data/`), 6 (randomness only through `IRandomSource`, one stream
  per concern) and 7 (commands are atomic and typed)

## Context

“รวมไอเทม: วางของที่ id และ tier เดียวกันทับกัน ได้ tier ถัดไป สูงสุด 3” was, until this stage, one
unambiguous command: two pieces folded, the survivor's tier went up, its footprint never changed.
The GDD's merge is *three* steps of grading (ธรรมดา → ปลุกเสก → มหาเวท), which leaves every merge of
a graded piece with a decision that the fold was making for the player — two tier-1 มีดหมอ are not
interchangeable once one of them could become “คมอาคม” and the other “คาถาไว”.

The stage's own words are “เลือก 1 จาก 3”: the merge of a piece that has upgrade variants asks the
player to pick one of `constant.upgrade_choice_count` choices, and the pick is what the survivor
keeps **for that step**. Everything else about the merge is unchanged — same id, same tier, the
survivor keeps its exact placement, one revision, one undo step.

The hard part is not the choice, it is **where the choices come from**. Three properties have to
hold at once, and the obvious implementation breaks all three:

1. **The offer must not be re-rollable.** A player who opens the chooser, backs out and opens it
   again must see the same three options; so must a player who merges, undoes the merge and merges
   again. A chooser that can be re-opened is a slot machine otherwise.
2. **The offer must be a function of the run, not of the UI's history.** It cannot depend on how
   many times a screen asked, or on how long the player looked at it, or a replay — which calls the
   commands, not the screen — would draw different options and diverge.
3. **The pick must be part of the recorded run**, because which variant was chosen changes the
   combat loadout the bag resolves to, and two bags with the same shapes can now resolve to
   different numbers.

## Decision

**1. One merge, two commands — one per situation.** `BackpackEditor.Merge(source, target)` is the
fold of a pair with **no** choices (charms, tomes, offerings, familiars, bags). When the piece has
choices it refuses with `inventory.merge_needs_variant` instead of choosing for the player, which is
what makes the chooser impossible to bypass. `BackpackEditor.ConfirmMerge(source, target, variantId,
masterSeed)` is the command that carries the pick. A pair with no chooser therefore still folds
through exactly the code path it always did, and “which id was chosen” is answerable from the
command that ran rather than from a side channel.

**2. A variant matches by category or tag, never by item id.** One `upgrade_variant` row covers a
family: an `ArcaneWeapon`-scoped row applies to every arcane weapon, a `fire`-scoped row only to the
pieces carrying that tag. Exactly one of the two columns is set (the shape `merge_recipe` uses for
its two optional ids), and a `Bag`-scoped row is refused by the data gate because variants are
matched against *item* rows and could never fire. A piece's pool is the union of the two, filtered
to the step's `from_tier`.

**3. The offer is a pure function, not a consumed stream.** `UpgradePool.DeriveSeed(masterSeed,
mergeOrdinal, itemId, fromTier)` mixes the run seed, a well-separated salt for
`RandomStreamKind.Upgrade`, the ordinal, the tier step and an FNV-1a hash of the item id, and that
seed drives a `Xoshiro128` which draws `constant.upgrade_choice_count` distinct variants from the
pool, weighted and without replacement. Asking twice about the same state returns the same choices
in the same order, so **cancelling the chooser and opening it again cannot shop for a better roll**.
Every input is in the key on purpose: the ordinal makes two merges of the same weapon offer
different choices, being part of the *undoable* state is what makes an undone-and-repeated merge
reproduce its own offer instead of re-rolling it, the item id keeps two different weapons from
sharing a draw, and the tier step keeps a 2 → 3 merge from repeating the 1 → 2 offer.

The run therefore gains a fourth random stream family, `RandomStreamKind.Upgrade`, that is
**derived from** rather than **drawn from** — no merge consumes the shop stream or the battle
stream, so the shop's stock and the battle's rolls cannot be perturbed by how many items the player
chose to fold (AGENTS rule 6).

**4. `ConfirmMerge` is atomic, and it re-computes the offer rather than trusting it.** The pair is
validated, the offer is recomputed from the *current* state, the id is checked against it, and only
then is anything written. A stale id, an id for another tier step, or an id from an offer that has
since been undone is refused with the backpack byte-identical (AGENTS rule 7): an empty id gives
`inventory.merge_variant_required`, an id outside the current offer gives
`inventory.merge_variant_not_offered`. `MergeOffer` is a **read model** — asking never changes
anything — which is what lets the screen, the preview and the tests all ask freely.

**5. The pick is kept on the survivor, one slot per completed step.** `Fold` appends the chosen id
to the survivor's per-step history (`ItemInstance.VariantIds`) and the step the *other* piece had
already chosen stays with the piece it belongs to: folding a tier-2 knife into another tier-2 knife
keeps the survivor's own step-1 pick, and the merge's pick becomes step 2. A step that had no
chooser still takes a slot, empty (`ItemInstance.NoVariant`), so step numbering cannot drift — a
2 → 3 merge of a piece whose 1 → 2 merge offered nothing must not read its own second choice as its
first. The history is what the loadout resolves from, so the slots are ordered, not a set.

**6. The choice is recorded, and the snapshot carries it.** `RunCommandKind.ConfirmMerge` (= 12,
**appended**, because the numbers of the recorded log are part of the format) carries the source,
the target and the chosen `VariantId`; the log's replay route calls `RunSession.ConfirmMerge`, which
re-draws from the run's own seed and counter and therefore offers the recorded id again. The
backpack snapshot goes to **schema version 2**, adding `MergeCount` (the draw key's ordinal, and
undoable state) and `BackpackPieceDto.VariantIds`. A save layer that stored only the last variant,
or only the tier, could not reproduce the numbers the player is playing with.

**7. A modifier may only touch a stat the battle already carries.** `UpgradeStat` mirrors the
stats of `CombatLoadoutSnapshot` (`WeaponLoadout`, `SummonLoadout`, `PlayerStatBlock`) one for one,
and `UpgradeAccumulator` applies them with one order — all `Add` summed, all `Percent` summed, all
`Multiply` folded, then `((base + add) * (1 + percent/100)) * factors`. The order is the rule, not
the CSV's row order, and a stat shared by the whole board combines every piece's modifiers *before*
the base value is touched (two “+20%” upgrades are +40%, not +44%). `LoadoutResolver` puts the
result into the snapshot once per backpack `Revision`, so `data/` cannot smuggle a new combat
mechanic in through an upgrade row, and the recompute policy is unchanged.

**8. The columns are strings, validated against one vocabulary.** `upgrade_variant` and
`upgrade_variant_modifier` carry `category` / `tag` / `stat` / `method` as plain strings, because a
Luban enum typo fails the whole generate step with a parse error where a string reaches
`UpgradeVocabulary` and comes back as a finding that names the row it is in. The runtime maps them
through that one table, and the data gate checks the rest: exactly one condition, a known category
or a tag some item carries, `weight >= 1`, sequential tiers within `constant.max_tier`, an existing
`name_key`, at least one modifier, no zero magnitude, and — the rule that spans tables — **for every
item and every step, the pool is either empty (the documented immediate-merge fallback) or at least
`constant.upgrade_choice_count` choices**, so “เลือก 1 จาก 3” can always be honoured.
`constant.upgrade_choice_count` is bounded to 2..5, because a chooser of zero choices is not a
chooser and a hand-typed 19 is not a chooser either.

## Alternatives considered

- **Consuming a shared random stream at merge time.** Rejected: it is the one implementation that
  breaks property 1. A stream advances when it is read, so a chooser that is cancelled and reopened
  would roll again — the player could shop for a better offer — and an undo that restored the
  backpack but not the stream position would desync a replay. Deriving the offer from the state
  costs one hash and removes the whole class of bug.
- **Letting the screen draw the choices.** Rejected: the offer would then depend on how many times
  the UI asked (property 2), the command would have to trust a value it did not compute, and the
  pooling rule would live in the Presentation layer (AGENTS rule 10).
- **Auto-picking option 0 when the player does not choose.** Rejected: it silently spends the
  player's decision, and it is what the refusal `merge_needs_variant` exists to prevent. The two
  commands are the honest shape: the fold is for the pair that has nothing to choose, the confirm is
  for the pair that has.
- **One command with an optional variant id.** Rejected: “a merge with an empty id” would mean two
  different things depending on data (fold, or a choice the player skipped), and the recorded log
  would carry an empty `VariantId` that cannot be told from “this step had no chooser”.
- **Storing only the most recent variant on the piece (or only the tier).** Rejected: a 2 → 3 merge
  needs step 1 as much as step 2, and the effect of each step is order-insensitive but *additive
  across steps* — dropping a slot changes damage. It would also make an undo that restores the
  counter but not the slots draw the wrong offer.
- **Matching a variant by item id.** Rejected: it would need one row per item per step, which is
  how the two shipped `fire`-scoped rows became one row covering every fire-tagged piece.
- **Putting the modifiers in code (a fixed table of upgrade kinds).** Rejected by AGENTS rule 5:
  the numbers are balance, they live in `data/`, and a new choice has to be a CSV row plus a
  localization line, not a C# edit.
- **Letting an upgrade row name any stat, including a new one.** Rejected: the vocabulary is
  deliberately the set the battle contract already reads, so an upgrade cannot introduce a mechanic
  the combat systems do not implement (the stage's own scope note: “ตัวปรับค่าอ้างได้เฉพาะค่าสถานะที่
  CombatLoadoutSnapshot มีอยู่แล้ว”).
- **Recording the offer itself in the log instead of the id.** Rejected: the offer is a function of
  state the log already rebuilds (seed + counter), so recording it would add bytes that a replay
  must then be *checked* against, instead of a state that reproduces it.

## Consequences

- **`MergeItems` and `ConfirmMerge` are two ends of one rule**, and the screen is what decides
  between them: the drop gesture previews as a merge that names the piece under the pointer, the
  presenter asks `OfferMerge`, a pair with choices opens the choice window (Stage 09g) and a pair
  without one folds immediately. The fold still refuses a pair with choices
  (`inventory.merge_needs_variant`) as a safety net, so a command that skips the question cannot
  quietly choose for the player — but the gesture itself is no longer refused, because refusing it
  would leave the player with a question and no way to answer it.
- **The three merge codes are separately localized** (`merge_needs_variant`, `merge_variant_required`,
  `merge_variant_not_offered`), because the player's next action differs: pick something, pick
  again, or the offer has moved on.
- **A run's merge count is now part of its identity.** It is undoable state, it is in the snapshot,
  and it is the reason a repeated merge reproduces its offer. The randomized invariant test and the
  undo tests now cover it.
- **`RandomStreamKind.Upgrade` exists** for a generator that is never drawn from — a fourth family
  whose only job is to keep the merge draw out of the shop's and the battle's streams. The
  stream-separation check in the harness still sees three streams, because this one is not a stream.
- **`data/` grew two tables** (10 variants, 12 modifiers) and 13 localization rows; the load test's
  pins moved with them (159 rows), and three broken-data fixtures pin the new findings
  (`upgrade-pool-too-small`, `upgrade-weight-not-positive`, `upgrade-stat-unknown`).
- **The loadout can no longer be predicted from the shapes alone.** Two identical layouts resolve to
  different numbers if their pieces chose differently, which is the point — and it is why
  `LoadoutResolverTests` and the replay test both fold through `ConfirmMerge` rather than `Merge`.
