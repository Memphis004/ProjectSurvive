#!/usr/bin/env python3
"""Register an approved portrait as a Portrait v2 override (Unity side).

Writes (only with --write):
  UnityProject/Assets/Resources/Avatar/Portraits/portrait_<disciple>.png  (copy)
  UnityProject/Assets/Resources/Data/portrait_overrides.json               (upsert one entry)
Never writes .meta files: Unity creates them on import. PortraitOverrideImporter sets
the Sprite/Single/alpha import settings when the folder is imported.

Default is --dry-run: prints what would change and exits 0. Pass --write to apply.
Hard stop: do not run this script without the user's explicit approval for the disciple/seed.

Usage
  python register_override.py --disciple d000 --png final/portrait_d000.png --dry-run
  python register_override.py --disciple d000 --png final/portrait_d000.png --write
"""
import argparse
import json
import pathlib
import shutil
import sys

import numpy as np
from PIL import Image

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[3]
UNITY = ROOT / "UnityProject" / "Assets"
PORTRAITS_DIR = UNITY / "Resources" / "Avatar" / "Portraits"
OVERRIDES = UNITY / "Resources" / "Data" / "portrait_overrides.json"
KEY = np.array([255.0, 0.0, 255.0])


def validate_png(path):
    """Same checks as portrait_postprocess.verify_only (no postprocessing here)."""
    problems = []
    im = Image.open(path)
    if im.size != (1024, 1536):
        problems.append(f"size {im.size} != (1024, 1536)")
    if im.mode != "RGBA":
        problems.append(f"mode {im.mode} != RGBA")
    if not problems:
        arr = np.array(im).astype(np.float64)
        d = np.sqrt(((arr[..., :3] - KEY) ** 2).sum(axis=2))
        solid_key = int(((d < 60) & (arr[..., 3] >= 64)).sum())
        cov = float((arr[..., 3] > 0).sum()) / (1024 * 1536)
        if solid_key:
            problems.append(f"{solid_key} solid near-magenta pixels remain")
        if not (0.05 <= cov <= 0.60):
            problems.append(f"alpha coverage {cov:.4f} outside 0.05-0.60")
    if path.stat().st_size <= 100_000:
        problems.append("file <= 100 kB")
    return problems


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--disciple", required=True, help="e.g. d000")
    ap.add_argument("--png", required=True, type=pathlib.Path)
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--dry-run", action="store_true", help="default")
    mode.add_argument("--write", action="store_true", help="apply the change (needs user approval)")
    args = ap.parse_args()

    if not args.disciple.isalnum():
        raise SystemExit(f"bad disciple id {args.disciple!r}")
    png = args.png if args.png.is_absolute() else ROOT / args.png
    if not png.exists():
        raise SystemExit(f"missing png: {png}")
    problems = validate_png(png)
    for p in problems:
        print("FAIL " + p)
    if problems:
        return 1

    resource = f"Avatar/Portraits/portrait_{args.disciple}"
    dest = PORTRAITS_DIR / f"portrait_{args.disciple}.png"
    table = json.loads(OVERRIDES.read_text(encoding="utf-8"))
    entries = table.get("overrides", [])
    existing = [e for e in entries if e.get("discipleId") == args.disciple]
    new_entry = {"discipleId": args.disciple, "portraitResourcePath": resource}
    if existing and existing[0] == new_entry and dest.exists():
        print(f"no change: {args.disciple} already registered -> {resource}")
        return 0
    print(f"copy  {png} -> {dest}")
    print(f"table {args.disciple}: {existing[0] if existing else '(new entry)'} -> {new_entry}")

    if not args.write:
        print("dry-run: nothing written. Re-run with --write after the user approves.")
        return 0

    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(png, dest)
    entries = [e for e in entries if e.get("discipleId") != args.disciple] + [new_entry]
    table["overrides"] = entries
    tmp = OVERRIDES.with_suffix(".json.tmp")
    tmp.write_text(json.dumps(table, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    tmp.replace(OVERRIDES)
    print("written. Next: open Unity, let PortraitOverrideImporter run, set Sprite/Single if needed, then verify.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
