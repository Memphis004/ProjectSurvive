> **GENERATED FROM data/ — DO NOT EDIT.** Regenerate with `tools/gen-wiki.ps1`; the wiki is not the source of truth for balance numbers.

# Content index (generated)

This directory is the **synthesised content layer** of the wiki: one page per kind of table, generated from `data/` by `tools/gen-wiki.ps1`. Every number here is a copy of a table value; when this page and `data/` disagree, `data/` is right. Read `../SCHEMA.md` first for how to use the wiki, `../INGEST.md` for how a stage report becomes a system note, and `../index.md` for the hand-written side.

## Pages

| Page | Answers questions about | Source tables |
| --- | --- | --- |
| [items.md](items.md) | what a piece is, what it costs, what it weighs, what it does, which combos and upgrades it joins | `item`, `bag`, `merge_recipe`, `shop_pool` |
| [skills.md](skills.md) | a cast behaviour and its numbers, and which pieces use it | `skill` |
| [synergies.md](synergies.md) | the five combo rules, and the actual pieces on each side of every rule | `synergy`, `item` |
| [upgrades.md](upgrades.md) | the merge-upgrade choices per tier and which pieces each choice can be offered for | `upgrade_variant`, `upgrade_variant_modifier` |
| [enemies.md](enemies.md) | a ghost's role and numbers, and the floors it appears on | `enemy`, `wave` |
| [floors.md](floors.md) | a floor's duration, boss and wave timeline (what spawns, when, how many) | `floor`, `wave` |
| [economy.md](economy.md) | the run's starting purse, shop prices, sell-back, heal and the tuning constants | `economy`, `constant` |

## What is in data/ right now

| Kind | Count | Where |
| --- | --- | --- |
| items | 11 | [items.md](items.md) |
| bags | 3 | [items.md](items.md) |
| skills | 5 | [skills.md](skills.md) |
| synergy rules | 5 | [synergies.md](synergies.md) |
| upgrade variants | 10 | [upgrades.md](upgrades.md) |
| enemies | 4 | [enemies.md](enemies.md) |
| floors | 5 | [floors.md](floors.md) |

Items per category: 4 ArcaneWeapon, 3 Charm, 1 SpellTome, 2 Offering, 1 Familiar.

## Rules for reading this layer

- Every page starts with the banner `GENERATED FROM data/ — DO NOT EDIT`.
- Edit `data/`, then run `tools/gen-wiki.ps1`; never edit a page under `content/` by hand.
- `tools/verify-wiki.ps1` fails when the committed pages differ from a fresh generation, when a link or anchor is broken, when a data id has no section, or when a `systems/` or `gotchas/` page is not linked from the wiki index.


A deliberately broken link: [missing page](does-not-exist.md).
