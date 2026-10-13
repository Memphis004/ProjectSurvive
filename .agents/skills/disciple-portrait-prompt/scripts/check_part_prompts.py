#!/usr/bin/env python3
"""Check assets/part_prompts.json against the game's avatar_parts.json.

Rules
- Every part whose slot is mapped in slot_map (and not in ignore_slots) needs an entry.
- An entry needs a non-empty "text" OR a "note" (locked / not expressible parts).
- Texts use the composer's lint: negation words and key-colour words are reported.
  Negation or colour hits are ERROR here because part text is inserted into a prompt
  that the composer sends to the edit model.
- Entries with no matching game part are reported as WARN (stale).

Exit 0 when no ERROR and no missing entry; 1 otherwise.
"""
import importlib.util
import json
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[3]  # .agents/skills/<skill>/scripts -> repo root
ASSETS = HERE.parent / "assets"
GAME_PARTS = ROOT / "UnityProject" / "Assets" / "Resources" / "Data" / "avatar_parts.json"


def load_composer():
    spec = importlib.util.spec_from_file_location("compose_portrait_prompt", HERE / "compose_portrait_prompt.py")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def main():
    table = json.loads((ASSETS / "part_prompts.json").read_text(encoding="utf-8"))
    game = json.loads(GAME_PARTS.read_text(encoding="utf-8"))["parts"]
    composer = load_composer()
    slot_map = table["slot_map"]
    ignore = set(table["ignore_slots"])
    entries = table["parts"]

    errors, warns = [], []
    mapped = [p for p in game if p["slot"] in slot_map and p["slot"] not in ignore]
    game_ids = {p["id"] for p in game}

    for p in mapped:
        entry = entries.get(p["id"])
        if entry is None:
            errors.append(f"MISSING entry for '{p['id']}' (slot {p['slot']})")
            continue
        text = (entry.get("text") or "").strip()
        note = (entry.get("note") or "").strip()
        if not text and not note:
            errors.append(f"'{p['id']}' has neither text nor note")
            continue
        if text:
            if composer.NEGATION_RE.search(text):
                errors.append(f"'{p['id']}' text has negation '{composer.NEGATION_RE.search(text).group(0)}'")
            if composer.CHROMA_RE.search(text):
                errors.append(f"'{p['id']}' text has key-colour word '{composer.CHROMA_RE.search(text).group(0)}'")
            for word in composer.DEFAULT_BANNED:
                if word in text.lower():
                    errors.append(f"'{p['id']}' text has banned word '{word}'")

    for pid in entries:
        if pid not in game_ids:
            warns.append(f"stale entry '{pid}' (not in avatar_parts.json)")

    for w in warns:
        print("WARN  " + w)
    for e in errors:
        print("ERROR " + e)
    print(f"checked {len(mapped)} mapped parts, {len(entries)} entries: {len(errors)} error(s), {len(warns)} warning(s)")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
