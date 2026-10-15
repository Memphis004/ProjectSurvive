#!/usr/bin/env python3
"""Measure how tall Thai text actually is on screen, in a screenshot of the real build.

Stage 09d item 2 wants Thai text to be at least 22 px tall at a 1920×1080 base. That is a claim
about pixels, so it is measured in pixels: this script crops the screen rectangles the *player
itself* published (tests/web/scripts/collect-geometry.mjs) out of a screenshot taken at the same
size, and reports the ink height of each line of text found inside them.

    node tests/web/scripts/collect-geometry.mjs http://127.0.0.1:8080/ 1920 1080 \\
        artifacts/stage09d/camp-geometry-1920x1080.json
    python tools/screen-text-metrics.py \\
        --image artifacts/stage09d/screens/web-camp-1920x1080-dsf1.png \\
        --geometry artifacts/stage09d/camp-geometry-1920x1080.json \\
        --region openBag --region statusBar --region shelfLock0 --region shelfLock3

A "line of text" is a run of consecutive rows inside the region that contain pixels differing from
the region's own background colour. Ink height is that run's pixel count, so it is comparable to a
font's pixel size directly. Regions are named exactly as the report names them; `shelfLock*` and the
other world regions hold world-space text and are labelled as such, because the canvas rule does
not govern them and mixing the two is how a layout claim becomes untrue.
"""

from __future__ import annotations

import argparse
import json
import statistics
from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parent.parent


def background_luminance(pixels: list[tuple[int, int, int]]) -> float:
    """The region's background: the most common luminance, which is what text is drawn on."""

    luminance = [0.2126 * r + 0.7152 * g + 0.0722 * b for r, g, b in pixels]
    return statistics.median(luminance)


def ink_rows(image: Image.Image, contrast: float) -> list[int]:
    """Rows holding at least one pixel that differs from the region's background."""

    width, height = image.size
    pixels = list(image.convert("RGB").getdata())
    background = background_luminance(pixels)
    rows: list[int] = []

    for y in range(height):
        row = pixels[y * width : (y + 1) * width]
        for r, g, b in row:
            if abs(0.2126 * r + 0.7152 * g + 0.0722 * b - background) >= contrast:
                rows.append(y)
                break

    return rows


def group_rows(rows: list[int], gap: int = 1) -> list[tuple[int, int]]:
    """Consecutive rows into runs; a run is one line of text."""

    runs: list[tuple[int, int]] = []
    for row in rows:
        if runs and row - runs[-1][1] <= gap:
            runs[-1] = (runs[-1][0], row)
        else:
            runs.append((row, row))

    return runs


def crop_region(image: Image.Image, rect: dict[str, float], canvas_height: float) -> Image.Image:
    """A published screen rectangle (origin bottom-left) as an image box (origin top-left)."""

    left = int(round(rect["x"]))
    right = int(round(rect["x"] + rect["width"]))
    top = int(round(canvas_height - (rect["y"] + rect["height"])))
    bottom = int(round(canvas_height - rect["y"]))

    left = max(left, 0)
    top = max(top, 0)
    return image.crop((left, top, min(right, image.width), min(bottom, image.height)))


def regions_by_name(geometry: dict, names: list[str]) -> list[dict]:
    found = []
    for name in names:
        for rect in geometry.get("rects", []):
            if rect.get("name") == name:
                found.append(rect)
                break
        else:
            raise SystemExit(f"the layout report has no region named '{name}'")

    return found


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--image", required=True, help="screenshot of the canvas")
    parser.add_argument("--geometry", required=True, help="geometry JSON for the same frame")
    parser.add_argument("--region", action="append", required=True, help="region name (repeatable)")
    parser.add_argument("--contrast", type=float, default=18.0, help="ink/background luminance gap")
    parser.add_argument("--json", default="", help="also write the figures to this path")
    arguments = parser.parse_args()

    image_path = (REPO / arguments.image).resolve()
    geometry_path = (REPO / arguments.geometry).resolve()

    image = Image.open(image_path)
    document = json.loads(geometry_path.read_text(encoding="utf-8"))
    geometry = document["geometry"]
    canvas_height = geometry["canvasHeight"]

    if image.height != int(canvas_height):
        raise SystemExit(
            f"{image_path.name} is {image.width}x{image.height} but the report says the canvas was "
            f"{geometry['canvasWidth']}x{canvas_height}: the shot and the layout are not the same frame"
        )

    print(f"{image_path.name} ({image.width}x{image.height}) regions from {geometry_path.name}")
    print(f"{'region':<18} {'layer':<7} {'rect w x h':<13} {'lines':<6} ink height px (per line)")

    results = []
    for rect in regions_by_name(geometry, arguments.region):
        region = crop_region(image, rect, canvas_height)
        lines = group_rows(ink_rows(region, arguments.contrast))
        heights = [bottom - top + 1 for top, bottom in lines]
        results.append(
            {
                "region": rect["name"],
                "layer": rect.get("layer", ""),
                "rect": {
                    "width": round(rect["width"], 1),
                    "height": round(rect["height"], 1),
                },
                "line_count": len(lines),
                "line_ink_px": heights,
                "max_ink_px": max(heights) if heights else 0,
            }
        )

        print(
            f"{rect['name']:<18} {rect.get('layer', ''):<7} "
            f"{round(rect['width'])} x {round(rect['height']):<7} "
            f"{len(lines):<6} {heights}"
        )

    if arguments.json:
        target = (REPO / arguments.json).resolve()
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(
            json.dumps({"image": arguments.image, "regions": results}, indent=2) + "\n", encoding="utf-8"
        )
        print(f"\nwrote {target.relative_to(REPO)}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
