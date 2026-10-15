#!/usr/bin/env python3
"""Measure and compare the Thai fonts in this project at a fixed display size.

Stage 09d asks two things a screenshot alone cannot answer: how big a Thai letterform
actually is at the size the UI uses, and whether one Thai font renders visibly smaller
than another at the *same* point size. Both are properties of the font file, so they are
measured from the file rather than eyeballed.

    python tools/font-metrics.py --out artifacts/stage09d/fonts

Outputs (under --out):
  font-metrics.json                      the numbers below, for the stage report
  thai-22px-dark.png                     the same string at 22 px in each font, camp colours
  thai-22px-light.png                    the same, on white
  thai-22px-scaling.png                  each font grown until its base glyph matches the
                                         project font's ink height at 22 px

The measurement is the ink bounding box PIL reports for a string, i.e. what a renderer
actually paints, not the font's declared metrics: Thai base glyphs sit at very different
heights in different faces, and that difference is the thing being judged.

Usage:
  python tools/font-metrics.py [--size 22] [--out <dir>]
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

REPO = Path(__file__).resolve().parent.parent

# The font every canvas in the project uses, and the one this stage asked about.
PROJECT_FONT = REPO / "UnityProject/Assets/Main/Fonts/Sarabun-Regular.ttf"
PROJECT_FONT_LABEL = "Sarabun-Regular (in use)"
COMPARED_FONT = REPO / "UnityProject/Assets/Resources/Fonts/THSarabunPSK.ttf"
COMPARED_FONT_LABEL = "THSarabunPSK (orphan asset)"

# A Thai UI line of the shape the camp draws, and the individual glyphs the measurements use.
SAMPLE = "จัดย่าม   กดค้างเพื่อย้าย   ราคา 120"
BASE_GLYPHS = "ก"            # ko kai: the body of a Thai letter, no ascender and no tone mark
ASCENDER_GLYPHS = "ป"        # po pla: the same body plus the tall stem the head letter carries
LATIN = "xA"                 # for x-height / cap-height, to compare against the Latin half

# The camp HUD's own colours, so the comparison is judged against the real background.
DARK = (18, 14, 12)
LIGHT = (255, 255, 255)
TEXT_ON_DARK = (235, 226, 204)
TEXT_ON_LIGHT = (20, 16, 14)


def layout_box(font: ImageFont.FreeTypeFont, text: str) -> tuple[int, int, int, int]:
    """The layout bounding box the font engine reports for `text` (FreeType metrics)."""

    return font.getbbox(text)


def ink_box(font: ImageFont.FreeTypeFont, text: str, pad: int = 8) -> tuple[int, int, int, int]:
    """The ink bounding box of `text` measured from the pixels a renderer actually paints.

    Rendering and looking at the result avoids trusting the font's declared metrics, which is
    precisely what differs between two Thai faces: the layout box can be identical while the
    painted glyph is half the height.
    """

    ascent, descent = font.getmetrics()
    width = int(font.getlength(text)) + pad * 2
    height = ascent + descent + pad * 2
    image = Image.new("L", (max(width, 1), max(height, 1)), 0)
    ImageDraw.Draw(image).text((pad, pad), text, font=font, fill=255)

    box = image.getbbox()
    if box is None:
        return (0, 0, 0, 0)

    return (box[0] - pad, box[1] - pad, box[2] - pad, box[3] - pad)


def ink_height(font: ImageFont.FreeTypeFont, text: str) -> int:
    box = ink_box(font, text)
    return box[3] - box[1]


def measure(path: Path, label: str, size: int) -> dict[str, object]:
    if not path.exists():
        raise SystemExit(f"missing font file: {path}")

    font = ImageFont.truetype(str(path), size)
    ascent, descent = font.getmetrics()

    return {
        "label": label,
        "file": str(path.relative_to(REPO)).replace("\\", "/"),
        "requested_px": size,
        "line_ascent_px": ascent,
        "line_descent_px": descent,
        # The body of Thai text: what decides whether a line reads as "small" on screen.
        "thai_body_ink_px": ink_height(font, BASE_GLYPHS),
        # The same measurement from the engine's own layout box, kept as a cross-check: if the two
        # ever disagree, the painted-pixel figure above is the one to believe.
        "thai_body_layout_px": layout_box(font, BASE_GLYPHS)[3] - layout_box(font, BASE_GLYPHS)[1],
        "thai_ascender_ink_px": ink_height(font, ASCENDER_GLYPHS),
        "thai_sample_ink_px": ink_height(font, SAMPLE),
        "thai_body_em": round(ink_height(font, BASE_GLYPHS) / size, 3),
        "latin_x_ink_px": ink_height(font, "x"),
        "latin_cap_ink_px": ink_height(font, LATIN),
        "sample_advance_px": round(font.getlength(SAMPLE), 1),
    }


def size_for_same_base_ink(path: Path, target_px: int, limit: int = 200) -> int | None:
    """The display size at which `path`'s Thai base glyphs are as tall as `target_px`."""

    if not path.exists():
        return None

    if target_px <= 0:
        return None

    for size in range(8, limit + 1):
        if ink_height(ImageFont.truetype(str(path), size), BASE_GLYPHS) >= target_px:
            return size

    return None


def render_row(
    image: Image.Image,
    y: int,
    path: Path,
    label: str,
    size: int,
    text: str,
    colour: tuple[int, int, int],
) -> None:
    draw = ImageDraw.Draw(image)
    font = ImageFont.truetype(str(path), size)

    draw.text((16, y + 6), label, font=ImageFont.truetype(str(path), max(11, size // 2)), fill=colour)
    draw.text((16, y + 34), text, font=font, fill=colour)

    # A hairline along the ink bottom of the Thai base glyphs: the eye reads the two fonts as "the
    # same size" or not by comparing where the letters sit, so it is drawn where ink ends.
    box = ink_box(font, BASE_GLYPHS)
    baseline = y + 34 + box[3]
    draw.line([(0, baseline), (image.width, baseline)], fill=(120, 90, 60), width=1)


def build_comparison(
    out: Path,
    size: int,
    background: tuple[int, int, int],
    colour: tuple[int, int, int],
    name: str,
    rows: list[tuple[Path, str, int]],
) -> Path:
    width = 1180
    row_height = 34 + size * 2 + 26
    image = Image.new("RGB", (width, row_height * len(rows) + 16), background)

    for index, (path, label, row_size) in enumerate(rows):
        render_row(image, 8 + index * row_height, path, f"{label} @ {row_size}px", row_size, SAMPLE, colour)

    target = out / name
    image.save(target)
    return target


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--size", type=int, default=22, help="the display size to judge (default 22)")
    parser.add_argument("--out", default="artifacts/stage09d/fonts", help="output directory")
    arguments = parser.parse_args()

    out = (REPO / arguments.out).resolve()
    out.mkdir(parents=True, exist_ok=True)

    project = measure(PROJECT_FONT, PROJECT_FONT_LABEL, arguments.size)
    compared = measure(COMPARED_FONT, COMPARED_FONT_LABEL, arguments.size)

    # The size each font would need to paint the same base-glyph ink as the project font does at
    # the requested size. This is the "still too small at 22 px?" answer in one number.
    project_base_ink = int(project["thai_body_ink_px"])
    compared_parity_size = size_for_same_base_ink(COMPARED_FONT, project_base_ink)
    project_parity_size = size_for_same_base_ink(PROJECT_FONT, project_base_ink)

    report = {
        "display_size_px": arguments.size,
        "fonts": [project, compared],
        "parity": {
            "metric": "thai_body_ink_px (ko kai)",
            "reference": {
                "font": PROJECT_FONT_LABEL,
                "body_ink_px": project_base_ink,
                "size_for_that_ink": project_parity_size,
            },
            "compared_size_for_same_body_ink": compared_parity_size,
            "compared_ratio": (
                round(compared_parity_size / arguments.size, 2) if compared_parity_size else None
            ),
        },
        "images": [],
    }

    render_rows = [
        (PROJECT_FONT, PROJECT_FONT_LABEL, arguments.size),
        (COMPARED_FONT, COMPARED_FONT_LABEL, arguments.size),
    ]

    report["images"].append(
        str(
            build_comparison(out, arguments.size, DARK, TEXT_ON_DARK, "thai-22px-dark.png", render_rows).relative_to(
                REPO
            )
        ).replace("\\", "/")
    )
    report["images"].append(
        str(
            build_comparison(out, arguments.size, LIGHT, TEXT_ON_LIGHT, "thai-22px-light.png", render_rows).relative_to(
                REPO
            )
        ).replace("\\", "/")
    )

    scaling_rows = [(PROJECT_FONT, f"{PROJECT_FONT_LABEL} (reference)", arguments.size)]
    if compared_parity_size:
        scaling_rows.append(
            (COMPARED_FONT, f"{COMPARED_FONT_LABEL} grown to match", compared_parity_size)
        )

    report["images"].append(
        str(
            build_comparison(
                out, arguments.size, DARK, TEXT_ON_DARK, "thai-22px-scaling.png", scaling_rows
            ).relative_to(REPO)
        ).replace("\\", "/")
    )

    metrics_path = out / "font-metrics.json"
    metrics_path.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    print(json.dumps(report, indent=2, ensure_ascii=False))
    print(f"\nwrote {metrics_path.relative_to(REPO)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
