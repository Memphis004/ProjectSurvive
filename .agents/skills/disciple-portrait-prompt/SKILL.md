---
name: disciple-portrait-prompt
description: Compose short-input, full-length image-edit prompts for Cultivation-Together disciple portraits (Qwen-Image-Edit via ComfyUI, magenta #FF00FF canvas, 1024x1536) for ANY disciple - female, male or elder - and register the approved result as a Portrait v2 override in the Unity project. Use this whenever the user wants to change the pose, face, hairstyle, accessories (earrings, hairpins, crowns, ribbons, carried items) or outfit of a disciple portrait (d000 Liu YiFeng, d001 Lin Feng, d002 Su Yan, d003 Elder Zhao, any recruit), start a new "round N" for a disciple, derive a portrait prompt from a disciple's AvatarAppearance / part ids (body_robe_azure, hair_topknot_long, acc_jade_crown...), use a previous round's image as the base, apply a reference image, or import a chosen seed into portrait_overrides.json. Also use for Thai requests such as "เปลี่ยนทรงผม", "เปลี่ยนหน้า", "เปลี่ยนชุด", "ใส่ต่างหู/กิ๊บ/มงกุฎ/ริบบิ้น", "เปลี่ยนท่ายืน", "สร้าง portrait ศิษย์", "ใช้รูปล่าสุดเป็น base", "นำ portrait เข้าเกม", and for LAYER work: "แยกเลเยอร์", "แยกชิ้นส่วน/part สำหรับ customization", "ใช้ photocraft", "ตัด magenta เป็น alpha", "ทำ part library ผม/ชุด/หัว". Use it instead of hand-writing a long prompt.
---

# Disciple portrait prompt composer (Cultivation-Together)

Fork of `portrait-edit-prompt` (female-only) generalised for the whole roster. The user types only what changes
(`hair: @topknot_long`, or just a disciple's part ids); `scripts/compose_portrait_prompt.py` builds the full prompt.
Five parts can change: `pose`, `face`, `hair`, `accessory`, `outfit`.

## What this skill produces (and what it does not)

A portrait made here is ONE painted full-body image. In the game it enters through **Portrait v2 overrides**
(`PortraitOverrideMap` -> `Resources/Data/portrait_overrides.json`): `AvatarRenderer` draws that single sprite
instead of the layered parts. Consequences you must tell the user when relevant:

- It is for **story / important disciples** (d000-d003 today). Recruits and everyone not in the table keep the
  layered `avatar_parts.json` portrait; this skill does not replace the part art.
- An override portrait does **not** change when the player edits parts in AvatarCustomization (the panel already
  warns). Part ids are only used here as the *brief*, never as a live link.
- It does not touch chibi or Spine. Look-alike is kept by writing the brief from the same part ids
  (Resemblance rule: sex, body, hair, head family, accessory, colours must match the other backends).

## Files

| Path | Role |
|---|---|
| `scripts/compose_portrait_prompt.py` | prompt composer (profiles female / male / elder_male, presets, `--parts`, lint) |
| `scripts/portrait_edit_round.py` | generic ComfyUI round runner (replaces per-round branches) |
| `scripts/register_override.py` | copy chosen PNG into Unity + upsert `portrait_overrides.json` |
| `scripts/photocraft_layers.py` | **layer mode**: magenta-keyed part images -> aligned RGBA layer PNGs + `.pcraft` via photocraft-cli, with numeric checks |
| `assets/layers.example.json` | manifest example for `photocraft_layers.py` |
| `scripts/check_part_prompts.py` | every game part has prompt text; texts are affirmative and key-colour safe |
| `assets/presets.json` | `profiles` (pronouns, keep phrases) + `presets` per profile (female presets are the originals, verbatim) |
| `assets/part_prompts.json` | game part id -> affirmative prompt text, `slot_map`, `ignore_slots` |
| `assets/recipe.example.json` | recipe example covering male, elder (from part ids) and female |

## Rules inherited from the female pipeline (do not break)

1. **Locked parts are named "the same ...", changed parts are removed from that sentence.** The script does this.
2. **Describe what is wanted, never what is unwanted.** cfg 1 makes `negative_prompt` inert; forbidden words get
   drawn. Lint warns on negation words and errors on `banned_words`.
3. **Stay away from colours near #FF00FF** (pink, magenta, fuchsia, rose, red-violet). Use blue-leaning violet,
   teal, green, white, gold, silver, black, navy; for red accents use *vermilion*.
4. **Thin or sheer fabric needs solid under-layers + a skin-tone note** (added automatically).
5. **Hair and accessories stay separate.** Hair shape in `hair`; pins, crowns, ribbons, earrings, carried items
   (e.g. gourd) in `accessory`.
6. **Pose stays standing, head upright, mid-thigh crop** (head height 240px, head centre (512,488)). A pose
   change redraws the whole body: generate 3-5 seeds.
7. **Thin floating ribbons leave fringe** on the key colour; the prompt asks for wide, tied-in ribbons.
8. **One change per round when possible.**

Round order: **pose -> outfit -> hair/face -> accessory last**; always use the previous approved image as `base`.

## Profiles

`--profile female | male | elder_male` (default female; inferred from `--parts` via the `head` part id).
Profiles carry pronouns and the KEEP phrases ("the same body proportions and curves" vs "...and build";
accessory keep = "earrings, hairpins and hair ribbons" vs "headwear, hair ornaments and carried items").
`elder_male` falls back to `male` presets and uses a grey ink-wash hair medium (white hair/beard).
With `--profile female` the output is **byte-identical** to the original `compose_edit_prompt.py`
(regression-tested; only the lint hint gained "vermilion").

Presets: `python scripts/compose_portrait_prompt.py --list`. Free text is allowed anywhere a `@preset` is.

## Brief from game state (`--parts`)

```
python scripts/compose_portrait_prompt.py --parts '{"body":"body_robe_azure","head":"head_male_01",
  "hair":"hair_topknot_long","accessory":"acc_jade_crown"}'
```
- `slot_map`: body->outfit, hair->hair, accessory->accessory, head+face_marking->face.
- `ignore_slots`: base, eyes, brows, mouth, nose, eyeshadow - portrait-only face detail that a single painting
  decides itself (and the chibi cuts by design).
- Explicit flags beat `--parts`. Unknown part id -> WARN (run `check_part_prompts.py`).
- `acc_none` has no text: removal cannot be said affirmatively without fighting KEEP; pass free text instead.
- After editing `avatar_parts.json` run `python scripts/check_part_prompts.py` (exit 1 on missing entries).

## Workflow

1. Decide items: disciple id, profile, which parts change, base image, optional reference + `ref_for`.
2. Write a recipe (see `assets/recipe.example.json`) or use flags. Run
   `python scripts/compose_portrait_prompt.py --recipe recipe.json --out prompts.json`.
   Fix every `ERROR`, think about every `WARN`. Show the prompt only if the user asks.
3. Get the user's approved image count, then preview:
   `python scripts/portrait_edit_round.py --items prompts.json --round-name r10 --group male
    --template art/portraits_v2/workflows/<baseline>.json --approved-images N --preview`
4. Same command without `--preview` (add `--prepare-only` to stop after writing workflows). Output goes to
   `art/portraits_v2/<group>_round_<name>/` with `generation_log.json`, raw images and contact sheets.
5. **CHECKPOINT A**: report size / near-magenta fractions from the log, show the contact sheet, stop.
6. After the user names the seed: run the existing postprocess (chroma key -> alpha, head height 240px,
   head centre (512,488)), then
   `python scripts/register_override.py --disciple d000 --png <postprocessed.png> [--dry-run]`.
7. In Unity: let it import, set Texture Type = **Sprite (2D and UI) / Single** (the loader uses
   `Resources.Load<Sprite>`), open DiscipleDetail for that disciple and confirm it renders.

## Layer mode (PhotoCraft) - build a swappable part library

Goal: drop-in layers for AvatarCustomization (hair_back, body, head, hair_front, accessory...) that line up on the
game canvas, instead of one painting per disciple. **PhotoCraft does not split a flat painting into parts by
itself.** Its selection tools (Select Subject, Object Selection) separate figure from background, not hair from
head from robe, and a flat painting has no pixels for the occluded parts (hair behind the head, body under the
robe, forehead under the fringe). So the parts are *generated* one slot at a time by the edit model, and
PhotoCraft does the deterministic work: key colour -> alpha, edge clean-up, ordering, per-layer export.

1. Approve one neutral base portrait (magenta, 1024x1536) for the part family.
2. For each variant (e.g. `hair_topknot_long`): run the normal change round first
   (`--hair @topknot_long`), approve the seed, then compose isolate prompts on that approved image:
   `python scripts/compose_portrait_prompt.py --profile male --isolate hair_back`
   (also `body | head | hair_front | accessory`; `--isolate` cannot be mixed with change slots).
   **Experimental**: not yet tried on the live model. Generate 3 seeds per slot and judge by number (step 4).
3. Collect the chosen isolate images, write a manifest (`assets/layers.example.json`; parts bottom -> top:
   hair_back, body, head, ..., hair_front, accessory; part id = layer name = file name).
4. `python scripts/photocraft_layers.py --manifest layers.json --work work --out out --reference approved.png --max-mismatch 0.02`
   - Per part: Layer From Background -> Color Range (#ff00ff, fuzziness 40) -> Clear -> Deselect -> Defringe 3 ->
     Colour Decontaminate 100/r2 -> rename. Measured on photocraft-cli 0.7.0 with a test shape: raw colour range
     leaves ~3500 key-tinted edge pixels, this recipe leaves 1. Do not drop the two matting steps.
   - Checks: full-canvas size, RGBA, top corners transparent, `near_key_px`, non-empty bbox, and with
     `--reference` a re-composite vs the approved portrait (`mismatch_fraction`; a head drifted by 40px scored
     0.058 against 0.0064 for a clean cut). Exit 1 on any FAIL.
   - Output: `<id>.png` per part (full canvas, alignment kept), `<disciple>.pcraft` (open it in PhotoCraft for
     hand fixes of hallucinated occluded areas), `layers_report.json`.
5. Import into `avatar_parts.json` + Resources is **not automated yet** (see gaps).

### PhotoCraft facts verified on 0.7.0 (Linux x86_64, v0.7.0 release tarball, sha256 matched)
- `photocraft-cli commands --json` lists 858 commands with parameter docs; ask it instead of guessing params.
- MCP server: `photocraft-cli mcp --automation-read-root <dir> --automation-write-root <dir>`; tools
  `command_list`, `command_run`, `command_batch`, `doc_inspect`, `render_preview`. Register for Claude Code:
  `claude mcp add photocraft -- photocraft-cli mcp --automation-read-root <dir> --automation-write-root <dir>`.
- **`file.export.layersToFiles` is refused in automation sessions (MCP / serve)**: "uses ambient filesystem
  paths". That is why the script saves `.pcraft` with the scoped `doc.save` and exports with a one-shot
  `photocraft-cli run master.pcraft --cmd file.export.layersToFiles`. Do not try to export layers over MCP.
- `document.activate` takes `{"document": index}` (not `index`); `engine.execute` takes `{"command", "params"}`.
- `layer.copyToDocument` keeps pixel positions (offset 0,0), so aligned inputs stay aligned.
- Exported layer files are named `<prefix>_<NNNN>_<layername>.png`, NNNN counting from the top layer.
- PhotoCraft calls itself early alpha; pin the version (the script warns when it is not 0.7.0).
- The script was exercised through `serve` (same engine and command registry as `mcp`) and one-shot `run`;
  the MCP stdio transport itself and Windows were **not** exercised.

## Runner notes

- Graph nodes: 4 base image, 6 prompt, 8 seed, 10 filename_prefix, 11 reference (only set when an item has `ref`;
  otherwise the template's node 11 image is kept and a NOTE is printed - the prompt never mentions image 3).
- `--approved-images` is mandatory and must be >= the number of jobs. Free local execution only
  (`workflow validate` must be valid and not spend credits).
- `--assets-dir` + `--comfy-input` copy base/ref images into the ComfyUI input folder.
- The legacy `portrait_female_candidates.py` rounds 1-7 are frozen history; new rounds never go through it.
- If the reference should supply hair/face and a *different* image the outfit, the graph needs a 4th image slot:
  check the `TextEncodeQwenImage21` schema first, otherwise split into two rounds.

## Hard stops

- Generate only what the user approved for this round; stop at CHECKPOINT A.
- Do not postprocess, register or import until the user names the seed.
- Report numbers from the log; never judge "beautiful" yourself.
- Do not edit `portrait_overrides.json` by hand and do not write `.meta` files by hand.

## Known gaps (verify before relying on them)

- Layer mode: isolate prompts and the claim that Qwen-Image-Edit keeps position/size when isolating a part are
  untested; the part-import step into `avatar_parts.json` (spritePath etc.) is not written because that schema
  has not been seen. `check_part_prompts.py` reads only `id`/`slot` for sure; its `isDefault`/`spritePath`/
  `chibiSheetPath` skip-rule for empty default parts is an assumption.

- New-from-scratch characters (no approved base yet) still start from the baseline workflow brief; this skill
  only covers edit rounds from an approved base. Male baselines for d000/d003 must exist in
  `art/portraits_v2/workflows/`.
- `portrait_edit_round.py` and the male/elder presets were exercised with a mock project and the composer
  tests, **not against a live ComfyUI**. Run one `--preview` and one single-image round first.
- DiscipleList card icons are baked by `AvatarIconBaker` from the *layered* parts; check whether the baker honours
  `PortraitOverrideMap` - otherwise the list icon will not match the override portrait.
- Already-approved female portraits (loose hair, ruqun/hanfu) do not match the placeholder part ids of d001/d002
  (`hair_short`, `body_robe_grey`...). Decide which side leads before using `--parts` for them. Also
  `MockSectData` d001 is Female but uses `head_male_01` (known validator report).