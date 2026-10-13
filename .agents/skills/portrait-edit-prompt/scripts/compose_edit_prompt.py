#!/usr/bin/env python3
"""Compose short-input, full-length image-edit prompts for portrait rounds.

Parts you can change: pose, face, hair, accessory (earrings / hairpins / hair ribbons), outfit.

Usage:
  python compose_edit_prompt.py --hair @updo --accessory @jade_flowers_ribbons --ref-for accessory
  python compose_edit_prompt.py --pose @hand_on_chest
  python compose_edit_prompt.py --outfit @violet_gauze --ref-for outfit
  python compose_edit_prompt.py --outfit @violet_gauze --accessory @lavender_ribbon --ref-for outfit,accessory
  python compose_edit_prompt.py --recipe recipe.json [--out prompts.json]
  python compose_edit_prompt.py --list

A slot value is free text or "@name" (a preset from assets/presets.json).
Parts that are NOT given are locked with "the same ..." in the KEEP sentence.
Parts that ARE given are removed from KEEP and described affirmatively in CHANGE.
Never submits a job; writes only the --out file you ask for.
"""
import argparse, json, re, sys
from pathlib import Path

PRESETS = json.loads((Path(__file__).resolve().parent.parent / 'assets/presets.json').read_text(encoding='utf-8'))
PARTS = ('pose', 'face', 'hair', 'accessory', 'outfit')
KEEP_PHRASE = {
    'pose': 'the same standing pose and hand position',
    'face': 'the same face',
    'hair': 'the same hairstyle',
    'accessory': 'the same earrings, hairpins and hair ribbons',
    'outfit': 'the same outfit',
}
REF_ROLE = {
    'outfit': 'the outfit colour mood, fabric and ornament design',
    'hair': 'the hairstyle',
    'face': 'the facial features',
    'accessory': 'the design of the earrings, hairpins and hair ribbons',
    'pose': 'the body pose',
}
DEFAULT_BANNED = ['braid', 'plait', 'cornrow']
NEGATION_RE = re.compile(r"\b(no|not|never|without|don't|do not|avoid|forbidden|isn't|none)\b", re.I)
CHROMA_RE = re.compile(r'\b(pink|magenta|fuchsia|hot pink|rose|purple-red|red-violet)\b', re.I)
SHEER_RE = re.compile(r'\b(sheer|gauze|chiffon|thin|transparent|bare|slit|see-through)\b', re.I)
ORNAMENT_RE = re.compile(r'\b(pin|hairpin|ribbon|ornament|flower|clip|comb|tassel|earring|jewel)', re.I)
RIBBON_RE = re.compile(r'\b(ribbon|streamer)', re.I)
BAD_POSE_RE = re.compile(r'\b(reclin\w*|lying|lies|sitting|seated|kneel\w*|crouch\w*|squat\w*|bending|bent over|leaning over|upside|full-length|walking away)\b', re.I)


def resolve(part, value):
    if value is None:
        return None
    if value.startswith('@'):
        name = value[1:]
        if name not in PRESETS[part]:
            raise SystemExit(f"unknown {part} preset '@{name}'. available: {', '.join(PRESETS[part])}")
        return PRESETS[part][name]
    return value.strip().rstrip('.')


def parse_ref_for(value):
    if not value:
        return []
    parts = [x.strip() for x in (value.split(',') if isinstance(value, str) else value) if x.strip()]
    for part in parts:
        if part not in PARTS:
            raise SystemExit(f"ref_for '{part}' is not one of {', '.join(PARTS)}")
    return parts


def lint(slots, banned, ref_for):
    problems = []
    for part, text in slots.items():
        if not text:
            continue
        m = NEGATION_RE.search(text)
        if m:
            problems.append(f"WARN {part}: negation word '{m.group(0)}'. Rewrite as what you WANT; negatives get pulled "
                            "into the image (cfg 1 makes negative_prompt inert).")
        m = CHROMA_RE.search(text)
        if m:
            problems.append(f"WARN {part}: colour '{m.group(0)}' is close to the #FF00FF key; it may be eaten by chroma key "
                            "or leave a halo. Use blue-leaning violet, teal, green, white, gold, silver, black, navy.")
        for word in banned:
            if re.search(word, text, re.I):
                problems.append(f"ERROR {part}: contains banned word '{word}'.")
    if slots.get('pose') and BAD_POSE_RE.search(slots['pose']):
        problems.append(f"WARN pose: '{BAD_POSE_RE.search(slots['pose']).group(0)}' breaks the upright, mid-thigh-cropped framing "
                        "that postprocess assumes (head height 240px, head centre (512,488)). Keep the pose standing with the head upright.")
    if slots.get('hair') and slots.get('accessory') is None and ORNAMENT_RE.search(slots['hair']):
        problems.append("WARN hair: describes ornaments (pin/ribbon/flower...) which clash with the locked 'same earrings, hairpins and "
                        "hair ribbons'. Move the ornaments to --accessory.")
    if slots.get('hair') and slots.get('accessory') and ORNAMENT_RE.search(slots['hair']):
        problems.append("WARN hair: also mentions ornaments while --accessory is set; the two descriptions may fight. Keep ornaments in accessory only.")
    if slots.get('accessory') and RIBBON_RE.search(slots['accessory']):
        problems.append("INFO accessory: thin ribbons floating against the magenta key often leave fringe after postprocess. "
                        "The prompt asks for ribbons tied into the hair and wide enough to read as solid shapes; check the edge numbers.")
    for part in ref_for:
        if not slots.get(part):
            problems.append(f"WARN: ref_for={part} but {part} is not being changed; the reference would fight the KEEP sentence.")
    return problems


def compose(item, banned=None):
    slots = {p: resolve(p, item.get(p)) for p in PARTS}
    changed = [p for p in PARTS if slots[p]]
    if not changed:
        raise SystemExit('nothing to change: give at least one of ' + '/'.join('--' + p for p in PARTS))
    ref_for = parse_ref_for(item.get('ref_for'))
    banned = DEFAULT_BANNED if banned is None else banned
    problems = lint(slots, banned, ref_for)

    keep = ['the same body proportions and curves'] + [KEEP_PHRASE[p] for p in PARTS if p not in changed]
    keep += ['the same mid-thigh crop', 'the same ink-brush painting medium',
             'and the same flat solid magenta #FF00FF background everywhere outside the figure']
    lines = ['Edit image 1. Keep the woman in image 1 exactly as she is: ' + ', '.join(keep) + '.']
    names = ', '.join(changed[:-1]) + (' and ' if len(changed) > 1 else '') + changed[-1]
    lines.append('Change ONLY her ' + names + '.')
    if slots['pose']:
        lines.append(f"Her pose is now {slots['pose']}. Her head stays upright at the same position and size in the frame, "
                     "and the figure stays centred with the same mid-thigh crop.")
    if slots['hair']:
        lines.append(f"Her hair is now {slots['hair']}. The hair is painted in the same black ink-wash and dry-brush style as image 1, "
                     "with a clean outline against the flat magenta background.")
    if slots['face']:
        lines.append(f"She now has {slots['face']}.")
    if slots['accessory']:
        text = (f"She now wears {slots['accessory']}. The ornaments are painted in the same ink-wash style as image 1 "
                "with crisp clean edges.")
        if RIBBON_RE.search(slots['accessory']):
            text += " Any ribbon is tied firmly into the hair and wide enough to read as a solid flowing shape against the flat magenta background."
        lines.append(text)
    if slots['outfit']:
        text = f"Her clothing is now {slots['outfit']}."
        if SHEER_RE.search(slots['outfit']):
            text += (" Thin fabric is painted with layered pale ink washes and fine dry-brush strokes in the same ink-wash style as image 1. "
                     "Wherever skin is visible through the cloth or a slit it is painted in natural warm skin tone, "
                     "so that the magenta background never shows through the cloth or the skin.")
        lines.append(text)
    if ref_for:
        roles = [REF_ROLE[p] for p in ref_for]
        role = roles[0] if len(roles) == 1 else ', '.join(roles[:-1]) + ' and ' + roles[-1]
        lines.append(f"Image 3 supplies only {role}; every other part of her and the background stay as in image 1.")
    lines.append('Only one character. No text, no logo, no watermark.')
    prompt = '\n'.join(lines)
    if len(prompt) > 2000:
        problems.append(f'WARN: prompt is {len(prompt)} chars; consider shortening the slot text.')
    return prompt, problems


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    for p in PARTS:
        ap.add_argument('--' + p)
    ap.add_argument('--ref-for', help='part(s) that image 3 (reference) supplies, comma separated, e.g. outfit,accessory')
    ap.add_argument('--recipe', help='JSON: {"banned_words": [...], "items": [{"id","seed","base","ref","pose","face","hair","accessory","outfit","ref_for"}]}')
    ap.add_argument('--out', help='write the composed items to this JSON file')
    ap.add_argument('--list', action='store_true', help='list presets')
    args = ap.parse_args()

    if args.list:
        for part in PARTS:
            print(f'[{part}]')
            for name, text in PRESETS[part].items():
                print(f'  @{name}: {text[:90]}{"..." if len(text) > 90 else ""}')
        return

    if args.recipe:
        recipe = json.loads(Path(args.recipe).read_text(encoding='utf-8'))
        items, banned = recipe['items'], recipe.get('banned_words')
    else:
        items = [{'id': 'single', **{p: getattr(args, p) for p in PARTS}, 'ref_for': args.ref_for}]
        banned = None

    results, failed = [], False
    for item in items:
        prompt, problems = compose(item, banned)
        print('=' * 78)
        print(f"{item.get('id')}  seed={item.get('seed')}  base={item.get('base')}  ref={item.get('ref')}")
        print(prompt)
        for p in problems:
            print('  ' + p, file=sys.stderr)
            failed |= p.startswith('ERROR')
        results.append({**item, 'prompt': prompt, 'lint': problems})
    if args.out:
        Path(args.out).write_text(json.dumps(results, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        print(f'wrote {args.out}')
    sys.exit(1 if failed else 0)


if __name__ == '__main__':
    main()