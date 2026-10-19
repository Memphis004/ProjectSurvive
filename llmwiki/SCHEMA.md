# llmwiki SCHEMA — how an agent uses and maintains this wiki

The wiki is the project's **synthesised knowledge layer**. It answers content
questions without making the reader re-derive them from `data/` and the stage
reports every time. It is not a second copy of the GDD, and it is **never the
source of truth**.

## 1. Three layers

| Layer | Paths | Written by | Rule |
| --- | --- | --- | --- |
| **Raw sources** | `data/`, `lib/`, `docs/gdd/`, `docs/stages/`, `docs/adr/` | stage work | read-only for the wiki; never edit one to match a wiki page |
| **Wiki** | `llmwiki/` — `index.md`, `glossary.md`, `systems/`, `gotchas/`, generated `content/` | this schema, `INGEST.md`, hand-written notes, `tools/gen-wiki.ps1` | reasons, context, links, gotchas; **no balance-number tables in hand-written pages** |
| **Schema** | this file + the pointer row in `AGENTS.md` | humans | read before editing the wiki; changes rarely |

**Hard rule (AGENTS rules 4 and 5):** a balance number in a wiki page is a
**copy**. Numbers come from `data/` and only from `data/`, through
`tools/gen-wiki.ps1` into `llmwiki/content/`. A hand-written page may explain
*why* a number exists and link to the page that prints it; it must not restate
the number. Player-facing Thai text comes from `data/localization/localization.csv`
by key; a missing key prints `(missing: <key>)`, never a guess.

## 2. Which page answers which question

| The question sounds like | Read |
| --- | --- |
| "มีไอเทมอะไรบ้าง / ไอเทมชิ้นนี้ทำอะไรได้ / ราคา น้ำหนัก รูปทรง" | [`content/items.md`](content/items.md) |
| "คาถานี้แรงแค่ไหน / cooldown / MP / ระยะ / ทะลุ" | [`content/skills.md`](content/skills.md) |
| "คอมโบ / วางติดกันแล้วเกิดอะไร / tag คู่ไหน" | [`content/synergies.md`](content/synergies.md) |
| "อัปเกรด tier 1→2 มีอะไรบ้าง / variant ไหนเข้าเงื่อนไขชิ้นไหน" | [`content/upgrades.md`](content/upgrades.md) |
| "ศัตรู / HP / ความเร็ว / ดรอปเท่าไร" | [`content/enemies.md`](content/enemies.md) |
| "ชั้น 3 มีผีอะไร กี่ตัว เมื่อไหร่ / ชั้นนี้ยาวเท่าไร" | [`content/floors.md`](content/floors.md) |
| "เริ่มด้วยเบี้ยเท่าไร / ขายคืนกี่ % / ค่าคงที่ของเกม" | [`content/economy.md`](content/economy.md) |
| "ทำไมออกแบบแบบนี้ / ระบบนี้ทำงานอย่างไร" | `docs/adr/` + [`systems/`](systems/) |
| "ติดปัญหา build / test / Unity / WebGL" | [`gotchas/`](gotchas/) |
| "ศัพท์ไทย ↔ ชื่อในโค้ด" | [`glossary.md`](glossary.md) |
| "ตัวเลขล่าสุดที่ต้องการความแน่นอน 100 %" | read `data/tables/*.csv` directly (then say so) |

Every page starts from [`index.md`](index.md) or [`content/index.md`](content/index.md).
Generated sections carry explicit anchors (`<a id="item.x">`), so a stable link
looks like `content/items.md#item.kali_flame_yant`.

## 3. The three cycles

**Query (read).** Open `index.md` → follow the routing row above → answer from the
pages, naming the pages used. If the question needs a number the page prints, quote
the page. If the question needs the *current* number with certainty, also open
`data/`. If the two disagree, **`data/` wins**: report the conflict, do not silently
pick the wiki and do not edit `data/` to match the wiki.

**Ingest (write a note).** Follow [`INGEST.md`](INGEST.md). One source report in;
update only the hand-written pages it changes; add a back-link to the source; log
the run in [`log.md`](log.md). Never hand-edit `content/`.

**Lint (check).** Run `tools/verify-wiki.ps1`. It regenerates `content/` into a
temp directory and fails when a committed page differs, when a relative link or
anchor is broken, when a data id has no section (or an anchor names no data row),
or when a `systems/`/`gotchas/` note is not linked from `index.md`. A heuristic
WARN flags hand-written pages that restate a skill's data numbers. `-Fixtures`
proves the lint rejects three deliberately broken copies. It is also the Fast
step `wiki: content reproducible`. The lint writes **no file inside the
repository** by default; `-WriteReport` opts in to
`artifacts/wiki/verify-wiki-report.json`.

## 4. When the wiki and data/ disagree

1. Believe `data/`.
2. Regenerate: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/gen-wiki.ps1`.
3. If a hand-written page caused the conflict, fix the page and say what changed.
4. Record the conflict in the stage report or `log.md`. Never edit `data/` to
   make a wiki page true — `data/` changes belong to a gameplay stage, not a
   documentation one.

## 5. Commands

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/gen-wiki.ps1            # regenerate content/
powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-wiki.ps1         # lint (exit 0/1/2)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-wiki.ps1 -Fixtures  # prove lint can fail
```
