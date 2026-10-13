---
name: portrait-edit-prompt
description: Compose short-input, full-length image-edit prompts for the XianXia female portrait pipeline (Qwen-Image-Edit via ComfyUI, magenta #FF00FF canvas). Use this whenever the user wants to change the pose/stance, face, hairstyle, accessories (earrings, hairpins, hair ribbons) or outfit/clothes of an already generated portrait (d001 Lin Feng, d002 Su Yan, etc.), use a previous round's image as the base, apply a reference image to the outfit, hair, face or accessories, or start a new "round N" in Tools/art/portrait_female_candidates.py. Also use for Thai requests such as "เปลี่ยนทรงผม", "เปลี่ยนหน้า", "เปลี่ยนชุด", "ใส่ต่างหู/กิ๊บ/ริบบิ้น", "เปลี่ยนท่ายืน/ท่าทาง", "ใช้รูปล่าสุดเป็น base", "แก้ prompt รอบใหม่". Make sure to use it instead of hand-writing a long prompt.
---

# Portrait edit prompt composer

The user only wants to type the part that changes (e.g. "hair: updo", "outfit: @violet_gauze").
Five parts can change: `pose`, `face`, `hair`, `accessory` (earrings, hairpins, hair ribbons), `outfit`.
`scripts/compose_edit_prompt.py` builds the full prompt; this file explains the rules behind it and how to wire it in.

## Why prompts are built this way (do not break these rules)

1. **Locked parts are named with "the same ...", changed parts are removed from that sentence.**
   Leaving "keep the same hair" in while describing a new hairstyle is the same bug as "two braids" vs "no braids":
   the model obeys the more specific affirmative clause. The script does this removal automatically.
2. **Describe what is wanted, never what is unwanted.** The graph runs at cfg 1, so `negative_prompt` is inert and a
   forbidden word (braid, ponytail...) pulls that thing into the image. The script warns on negation words and
   errors on `banned_words`.
3. **Avoid colours near the key colour #FF00FF** (pink, magenta, fuchsia, rose, red-violet) in hair or clothes.
   Use blue-leaning violet, lavender, black, brown, silver, navy. The script warns on these.
4. **Thin or sheer fabric needs solid under-layers and a skin-tone note** so the magenta never shows through cloth
   or skin; the script appends this sentence automatically when sheer words appear.
5. **Keep hair and accessories separate.** Describe hair shape in `hair` and every pin, flower, ribbon and earring in
   `accessory`. If hair text also names ornaments, they fight the locked/changed accessory sentence (the script warns).
6. **Pose must stay standing, head upright, mid-thigh crop.** postprocess assumes head height 240px and head centre
   (512,488), so reclining/sitting/kneeling/bending poses are linted. A pose change redraws the whole body, so clothes
   and face drift more than in other rounds: generate 3-5 seeds and pick the one closest to the base.
7. **Ribbons and fine ornaments:** thin ribbons floating on the magenta key leave fringe after postprocess, so the
   prompt asks for ribbons tied into the hair and wide enough to read as solid shapes. Earrings are tiny at portrait
   scale (head ~240px) and nearly invisible in the baked icon; set expectations with the user.
8. **Change one thing per round when possible.** If the result is bad you can tell which change caused it, and good
   seeds are not lost. Multi-part changes are allowed but say so to the user.

## Recommended order of rounds

Do changes in this order, because each step redraws everything below it: **pose -> outfit -> hair/face -> accessory last.**
Accessories are the smallest details and are lost or redrawn by any later round, so finish them last. Always use the
approved image of the previous step as the new base (`image_1`).

## Reference images

- One reference image (image 3) can supply several parts: `--ref-for outfit,accessory`. A reference for hair/accessory
  and a different reference for outfit needs a 4th image slot (see wiring notes) or two separate rounds.
- Before using a reference, crop out watermarks and UI text (for example the corner tag on some AI-generated
  images). The model may copy visible text into the picture.
- Realistic or painterly references are fine: they supply design (shape, colour, ornament layout) only; the prompt
  states that the ink medium of image 1 is kept.
- Reference colours must stay away from magenta/pink. Greens, teals, whites, golds and silvers are safe.

## Workflow

1. Decide the items: for each image to generate, which parts change (`face`, `hair`, `outfit`), which base image,
   which reference image (if any) and which part that reference is for (`ref_for`).
2. List presets with `python scripts/compose_edit_prompt.py --list`. Use `@name` for a preset or free text.
   To add a reusable look, edit `assets/presets.json` (keep text affirmative).
3. Write a recipe (see `assets/recipe.example.json`) or run one-off with flags:
   `python scripts/compose_edit_prompt.py --hair @updo`
   `python scripts/compose_edit_prompt.py --outfit @violet_gauze --ref-for outfit`
   `python scripts/compose_edit_prompt.py --accessory @jade_flowers_ribbons --ref-for accessory`
   `python scripts/compose_edit_prompt.py --pose @hand_on_chest`
4. Fix every `ERROR` and think about every `WARN` before using the prompt. Show the user the composed prompt only
   if they ask; otherwise just tell them which parts were changed/locked.
5. Run with `--out prompts.json` and feed the prompts into the round definition of
   `Tools/art/portrait_female_candidates.py` (see below).

## Wiring into portrait_female_candidates.py

Only needed once per new round N (never reuse `>= 7` branches: `strip_affirmative_hair_clauses` raises if the
old outfit clause is absent, so gate it with `== 7`).

- `ROUNDN_ITEMS`: load `prompts.json` (or paste the items) and use each item's `seed`, `base`, `ref`, `prompt`.
- In `prepare()` for round N: `graph['4']['inputs']['image'] = item['base']` (base image replaces the blank
  magenta canvas), `graph['11']['inputs']['image'] = item['ref']` if a reference is used, and
  `prompt = item['prompt']` (overwrite the whole prompt, no `.replace` chains).
- Copy base/ref files into the ComfyUI input folder under the names used in the recipe.
- Add round N to argparse `choices`, `previous_round_images`, `style_reference`, `quota_authorization`, and the
  `__main__` block (`ART`, `LOG`, `SEEDS`).
- Seeds: generate 3 seeds per item when only hair or outfit changes (denoise is 1, so the face may drift;
  pick the seed whose face is closest to the base).
- If the reference is used for hair or face, and the outfit must also come from a different reference, the graph
  needs a 4th image slot. Check the `TextEncodeQwenImage21` schema first; if it accepts only three images, split
  into two rounds and use the first round's output as the next base.

## Hard stops (project rules still apply)

- Generate only what the user approved for this round; stop at CHECKPOINT A and let the user choose.
- Do not postprocess or import until the user names the seed.
- After generation, report the near-magenta fraction and size from the log; do not judge "beautiful" yourself.