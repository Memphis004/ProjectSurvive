# ADR 0010 — Item-local effects (charm passive, offering consumable) stay out of the item table for now

> **Superseded by [ADR 0028](0028-item-effects-and-status-table.md) (Stage 11a).** The charm and
> offering columns now exist in `data/tables/item.csv` together with their consumers. This record
> is kept as the reason they were deferred.

- **Status:** Superseded (Stage 11a)
- **Date:** 2026-10-14

## Context

The GDD's item table gives two categories an effect that is **local to the piece**
rather than a relationship between pieces:

| Category | GDD wording |
| --- | --- |
| เครื่องราง (`Charm`) | "บัฟ passive" — a passive buff |
| ของเซ่น (`Offering`) | "ใช้ได้จำกัดครั้ง ฟื้นฟูหรือเพิ่มเบี้ย" — limited uses, restores or grants bia |

The `item` table as Stage 04 inherits it has `category`, `element`, `price`, `weight`,
`skill_id`, `tags` and `footprint` — and no column for either. The five combo rules
(`synergy`) are tag-matched *relationships*, so they can express the curse-vs-holy
weakening and the offering-feeds-the-familiar rule, but they cannot express "this
charm grants +8% damage on its own".

AGENTS rule 5 says every balance number lives in `data/` and never in code, which
means an effect magnitude cannot simply be invented later in C#. AGENTS rule 12 says
not to build a structure for a feature that has no reason to exist yet.

## Decision

**Stage 04 does not add effect columns to `item`.** The three charms and the two
offerings carry tags that make them real participants in the combo system:

- `phra_amulet` `amulet|holy`, `waist_yant` `yant|holy`, `tiger_fang` `fang|holy` — each
  is a legal partner for `synergy.curse_holy_debuff` and/or `synergy.takrut_yant_cooldown`.
- `red_soda` and `boiled_egg` carry `offering` — the partner for
  `synergy.offering_familiar_speed`.

The magnitudes of the piece-local effects are **not authored yet, and must not be
written into code when they are**: the columns have to land in `data/tables/item.csv`
first, in the same commit as their consumer.

This is the "simplest path that does not close a door" (rule 12). It is deliberately
*not* a claim that charms and offerings are finished.

## Why not now

The consumer of both numbers belongs to a stage that does not exist yet:

- a charm's passive is applied by the **loadout aggregation** (`CombatLoadoutSnapshot`),
  which is Stage 05–07;
- an offering's uses and its heal/bia amount are consumed by the **run and consumable
  pipeline**, which is Stage 06.

Adding the columns now would fix a shape — how a stat modifier is named, whether
"uses" is per-run or per-piece, whether bia and HP share one field — before the code
that reads it can say what the shape should be. A data schema changed once is cheap; a
schema changed once *on a guess* is a migration with no evidence behind it.

## Consequences

- **A readability cost the reviewer should know about**: in Stage 04's `item.csv` a
  charm's own effect is invisible. It is visible only in `synergy.csv`. The stage
  report records this as a known gap rather than leaving it to be discovered.
- **A hard constraint carried forward**: whoever lands Stage 05/06 must add the effect
  columns (or an equivalent data table) *before or with* the code, never as a constant
  in Core. If a number has to be typed into a `.cs` file to make the feature work, this
  ADR has been violated.
- **Nothing to migrate**: no save format reads `item` yet (Stage 12), so adding the
  columns later is a `SchemaVersion` bump and a regeneration, not a data migration.
- **The rule set already enforces the piece half**: `item.footprint` must be non-empty
  and every piece must reach tier 3, so an inert *shape* cannot ship; an inert *effect*
  is the part still open.
