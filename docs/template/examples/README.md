# Examples — the game the template came from

Everything under this folder is **archived game material**. It is kept because the
reasoning is still useful and because deleting it would erase the evidence behind
decisions the template inherited; it is **not** part of the template's contract.

> **Read these as examples, not as rules.** A file here may name a system, table,
> scene or API that does not exist in the template. Nothing here is compiled, run or
> linked from the live docs; `tools/check-links.mjs` deliberately skips this folder.

## What is here

| Folder | Contents | Why keep it |
| --- | --- | --- |
| [`adr/`](adr/) | thirteen ADRs (0007, 0009, 0010, 0013–0021, 0023) | each solved a concrete problem a game will meet again: a contract seam, a data format, an atomic-command boundary, a read model |
| [`systems/`](systems/) | six system notes (bag rules, battle contract, battle simulation, battle presentation, camp screen, run + shop) | worked examples of the note format, and of what "documented enough" looked like |
| [`gotchas/`](gotchas/) | `ecslite-pool-refs.md` | the lesson belongs with the ECS engine that left the template |
| [`glossary-game.md`](glossary-game.md) | the game's Thai ↔ code-name table | shows the shape a game's own glossary should take |
| [`game-gdd-and-extraction-plan.md`](game-gdd-and-extraction-plan.md) | the game's design document plus the plan this template was extracted by | the source of the classification in [`../inventory.md`](../inventory.md) |

## What is *not* here

The template's own decisions are in [`../../adr/`](../../adr/) (numbers 0001–0099) and
its own system notes are in [`../../../llmwiki/systems/`](../../../llmwiki/systems/).
If a question is about the template, the answer must be one of those; a file in this
folder cannot override it.

## If a game wants one of these

Copy the **idea**, not the file: a game's decision gets its own ADR starting at
**0100** (AGENTS rule 12) and its own system note under `llmwiki/systems/`. Do not
link a live document back into this folder — that is how an example turns into a
rule nobody decided.
