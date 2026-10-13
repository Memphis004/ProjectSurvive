---
name: chroma-key-portrait-pipeline
description: "Generate, approve, cut out and ship chroma-keyed character portraits through local ComfyUI into Unity. Use when producing art on a magenta #FF00FF canvas (portraits, icons, sprites), driving a multi-round 'candidates then user picks' iteration loop, or when a diffusion model keeps producing a detail the prompt forbids. Covers the prompt-contradiction trap that negations cannot fix, honest quota accounting, measurable acceptance criteria instead of taste claims, and the post-process/import/bake-verify chain."
---

# Chroma-Key Portrait Pipeline (magenta-canvas → Unity)

A working recipe for the "generate N candidates, human picks, post-process, import, verify" loop, distilled from a 7-round / 28-image session where the same mistake burned five rounds.

## 0. The trap that cost five rounds: a prompt cannot contradict itself

**Symptom.** The user says "why does it *keep* giving me a braid / bare shoulders / whatever, I keep forbidding it". Rounds keep adding stronger prohibitions and nothing changes.

**Cause.** The prompt contains BOTH an affirmative instruction and a prohibition, e.g.

```
Draw this subject: Lin Feng, ..., black hair in two clearly visible braids, ...
Hairstyle variant A: long loose flowing hair. ABSOLUTELY NO BRAID.
```

Two amplifiers made it unfixable:
1. **`negative_prompt` is inert at cfg 1.** `TextEncodeQwenImage21` in the baseline graph runs cfg `1` / euler / simple. The negative string is stored and does nothing. Every prohibition must live in the positive prompt.
2. **Affirmative beats negative, always.** "two clearly visible braids" is concrete, countable, and located. A negation is neither. Repeating the forbidden word makes it *more* likely, because you keep naming it.

**Fix — delete, never contradict.** Strip the affirmative clause out of the shared subject brief. Then assert it is gone:

```python
FORBIDDEN_RE = re.compile(r'braid|plait|cornrow|ponytail|topknot|braided|\bbun\b', re.I)
BRIEF_START, BRIEF_END = 'Draw this new subject:', 'Exactly one SMALL accent color:'

def assert_subject_brief_clean(prompt):
    s, e = prompt.find(BRIEF_START), prompt.find(BRIEF_END)
    if s < 0 or e < 0 or e < s:
        raise RuntimeError('brief markers not found; refusing to submit')
    found = sorted({m.group(0) for m in FORBIDDEN_RE.finditer(prompt[s:e])})
    if found:
        raise RuntimeError(f'brief still dictates a banned feature ({found})')
```

Run this **before writing the workflow file**. After the fix, all four round-7 images had no braid at all.

**Generalise.** The same bug shape bites garments. If the user says "use the outfit from *this reference*", delete the outfit clause from the brief too — otherwise the brief and `image_3` fight and you get a blend of both.

**Rule of thumb.** Whatever the reference image or the per-variant sentence decides, the shared brief must say *nothing* about that dimension. One owner per dimension.

## 1. Verify the contradiction before touching anything else

Dump the actual prompts from disk and diff them. This took 2 minutes and ended a multi-round guessing game:

```python
import json, glob, os
for f in ['art/.../workflows/d001_seed430101.json'] + sorted(glob.glob('art/.../female*/workflows/d001_*.json')):
    p = json.load(open(f, encoding='utf-8'))['6']['inputs']['prompt']
    s, e = p.find('Draw this new subject:'), p.find('Exactly one SMALL accent color:')
    print(f, 'brief-says-braid=', 'two clearly visible braids' in p[s:e],
          'prompt-also-forbids=', 'NO BRAID' in p)
```

The output showed the clause present in **all 18 d001 prompts ever generated**, and a direct contradiction in 4 of them. Quote the file path and line to the user — "I wrote a prompt that asks for two things at once" is fixable; "the model ignores me" is not.

## 2. Reference images carry the weight text cannot

- Pick reference files the **user names**. Ask for the filename; do not guess.
- If the user's chosen reference contains the forbidden feature, using it as `image_3` will drag it back. Say so and offer: different reference / no reference at all.
- When the user supplies an approved output as the new reference, back it up and state the copy-back risk explicitly.

## 3. Honest quota accounting

When a stop is requested mid-loop, a killed shell does **not** kill its already-loaded Python child — jobs keep submitting. This is how a session shipped 24 images against a 12-image authorisation.

- Count submissions from the log, not from what the user saw.
- Persist `generation_requests_submitted` **before** and **after** every submit so an interrupted run reconciles.
- A sentinel file (`pause_requested`) checked at the top of each loop iteration stops the *next* submit, never the current one.
- Report the real overrun plainly. Do not round it away.

## 4. Acceptance criteria must be measurable

Taste is the user's call. Numbers are yours. Never write "looks good".

Measure framing, not vibes. Threshold the coverage so single stray pixels do not register as a figure:

```python
cov = fg.mean(axis=1)          # fg = max-channel distance from magenta > 20
rows = np.where(cov > 0.004)[0]
rows = rows[rows > 6]           # drop the row-0 PNG save artefact
```

Round 7 failed two invariants that only numbers exposed — head top at 4–7 % of canvas height against a 24 % target, and figures running to 94–99 % of the canvas (full-length, not the required mid-thigh crop). A glance would not have caught that.

Report a table: `seed | measured | target | pass/fail`.

## 5. Inspect before you trust

Crop and upscale the region that matters and actually look at it. A full 1024×1536 contact sheet hides a small braid; a head crop at 2× with a 10 px grid does not.

```python
crop = im.crop((450, 150, 680, 340)).resize((460, 380), Image.LANCZOS)   # 2x
```

Draw absolute-coordinate labels on the grid so measurements can be read straight off it. Do not claim you cannot see images — try first. (In this session the agent assumed it could not view images for many rounds and was wrong.)

## 6. Post-process: measure the head, do not guess

`Tools/art/portrait_postprocess.py` scales each portrait so head height hits a constant and the head centre lands on a fixed target. It needs three per-image numbers, read off a labelled grid:

| key | meaning |
|---|---|
| `hair_top` | top of the hair mass (exclude hairpin) |
| `chin` | chin |
| `face_cx` | x centre of the face oval at eye level |

State the tolerance (±5 px). The script's own checks must all pass: 1024×1536, RGBA, zero near-magenta pixels, alpha coverage 0.05–0.60, file > 100 kB.

Always run `neutralize_canvas` **after** the resize — Lanczos blends edge pixels and brings the magenta back.

Back up the previous output before overwriting (`final/_backup_*/`).

## 7. Import → bake → verify, in that order

1. Copy the PNG over the Unity asset. **Hash both sides** to prove the copy.
2. Leave the `.meta` untouched — it carries `textureType: 8`, `spriteMode: 1`, `alphaIsTransparency: 1`. Deleting it loses alpha handling.
3. `Assets/Refresh`.
4. Run the icon baker (`Xianxia/Generate Disciple List Icons`).
5. **Hash every icon before and after.** The baker rewrites all of them; only the changed one should differ. Identical hashes elsewhere prove determinism and that no other disciple was disturbed.
6. `Resources.Load` both sprites and print size + format.
7. Resolve the override map: confirm the changed id resolves and a disciple with no entry still returns empty (layered fallback intact).
8. Run the EditMode suite. Report `passed/total` honestly.
9. Play mode → screenshot → **look at it**.

## 8. Checkpoint discipline

Stop and let the human pick before post-processing, importing or baking. Present: file paths, seeds, measured numbers, contact sheets. Never auto-advance past an approval gate, and never merge an unapproved candidate into the project.

## Pre-flight checklist

- [ ] No affirmative/prohibition pair anywhere in the prompt
- [ ] Brief says nothing about any dimension the reference or variant owns
- [ ] Guard runs and passes **before** the workflow file is written
- [ ] Every workflow `valid=true, error_count=0, warning_count=0, spends_credits=false`
- [ ] Image `1024×1536 RGBA`, distinct sha256
- [ ] Framing invariants measured and reported as a table
- [ ] Region-of-interest crops actually inspected
- [ ] Submission count read from the log and reconciled
- [ ] Post-process checks all green, old output backed up
- [ ] Unity copy hash-verified, `.meta` preserved
- [ ] Icon hashes: only the intended one changed
- [ ] `Resources.Load` OK, fallback path intact
- [ ] Test suite run and reported
- [ ] Game View screenshot inspected

## Pitfalls that cost real time

| pitfall | consequence | guard |
|---|---|---|
| Prohibiting instead of deleting | 5 wasted rounds | `assert_subject_brief_clean` |
| Forgetting the brief-vs-`image_3` garment conflict | blended outfit | delete the clause too |
| Casing-sensitive `str.replace` on prompt text | strip silently no-ops | `re.sub(..., re.IGNORECASE)` |
| Guard demanding every variant's clause | blocks on the first disciple | count matches, require exactly 1 |
| Noise-sensitive foreground measurement | "figure fills 100 % of canvas" | threshold row coverage, drop row 0 |
| Trusting `kill` to stop the generator | quota overrun | sentinel + log-first accounting |
| Claiming visual quality | user has to re-check everything | numbers + paths only |