#!/usr/bin/env python3
"""Compose short-input, full-length image-edit prompts for Cultivation-Together disciple portraits.

Generalised from compose_edit_prompt.py (female-only). With --profile female the output is
byte-identical to the original script (regression-tested), so approved female rounds stay reproducible.

Parts you can change: pose, face, hair, accessory, outfit.

Usage:
  python compose_portrait_prompt.py --profile male --hair @topknot_long --accessory @jade_crown
  python compose_portrait_prompt.py --profile female --hair @updo --accessory @jade_flowers_ribbons --ref-for accessory
  python compose_portrait_prompt.py --parts '{"body":"body_robe_azure","hair":"hair_topknot_long","accessory":"acc_jade_crown"}' --profile male
  python compose_portrait_prompt.py --recipe recipe.json [--out prompts.json]
  python compose_portrait_prompt.py --list

A slot value is free text or "@name" (a preset of the chosen profile, falling back to the profile's parent).
--parts maps game part ids (assets/part_prompts.json) to slots; explicit flags win over --parts.
Parts that are NOT given are locked with "the same ..." in the KEEP sentence.
Never submits a job; writes only the --out file you ask for.
"""
import argparse, json, re, sys
from pathlib import Path

ASSETS = Path(__file__).resolve().parent.parent / 'assets'
_P = json.loads((ASSETS / 'presets.json').read_text(encoding='utf-8'))
PROFILES, PRESETS, ISOLATE = _P['profiles'], _P['presets'], _P.get('isolate', {})
PART_PROMPTS = json.loads((ASSETS / 'part_prompts.json').read_text(encoding='utf-8'))
PARTS = ('pose', 'face', 'hair', 'accessory', 'outfit')
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


def profile_chain(name):
    if name not in PROFILES:
        raise SystemExit(f"unknown profile '{name}'. available: {', '.join(PROFILES)}")
    chain = [name]
    while PROFILES[chain[-1]].get('fallback'):
        chain.append(PROFILES[chain[-1]]['fallback'])
    return chain


def resolve(profile, part, value):
    if value is None:
        return None
    if value.startswith('@'):
        chain = profile_chain(profile)
        name = value[1:]
        for group in chain:
            text = PRESETS.get(group, {}).get(part, {}).get(name)
            if text:
                return text
        avail = sorted({n for g in chain for n in PRESETS.get(g, {}).get(part, {})})
        raise SystemExit(f"unknown {part} preset '@{name}' for profile '{profile}'. available: {', '.join(avail)}")
    return value.strip().rstrip('.')


def infer_profile(parts):
    head = (parts or {}).get('head', '')
    if 'female' in head:
        return 'female'
    if 'elder' in head:
        return 'elder_male'
    return 'male'


def parts_to_slots(parts):
    """Map {game_slot: part_id} to prompt slots. Returns (slots, warnings)."""
    slots, warns = {}, []
    smap, ignore, table = PART_PROMPTS['slot_map'], set(PART_PROMPTS['ignore_slots']), PART_PROMPTS['parts']
    for game_slot, part_id in (parts or {}).items():
        if not part_id or game_slot in ignore:
            continue
        if game_slot not in smap:
            warns.append(f"WARN parts: game slot '{game_slot}' has no prompt slot; ignored.")
            continue
        entry = table.get(part_id)
        if entry is None:
            warns.append(f"WARN parts: part id '{part_id}' has no entry in part_prompts.json; slot '{game_slot}' ignored "
                         "(run check_part_prompts.py).")
            continue
        if not entry.get('text'):
            warns.append(f"WARN parts: '{part_id}' has no prompt text ({entry.get('note', 'not expressible')}).")
            continue
        target = smap[game_slot]
        slots[target] = entry['text'] if target not in slots else slots[target] + ' and ' + entry['text']
    return slots, warns


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
                            "or leave a halo. Use blue-leaning violet, teal, green, white, gold, silver, black, navy, vermilion.")
        for word in banned:
            if re.search(word, text, re.I):
                problems.append(f"ERROR {part}: contains banned word '{word}'.")
    if slots.get('pose') and BAD_POSE_RE.search(slots['pose']):
        problems.append(f"WARN pose: '{BAD_POSE_RE.search(slots['pose']).group(0)}' breaks the upright, mid-thigh-cropped framing "
                        "that postprocess assumes (head height 240px, head centre (512,488)). Keep the pose standing with the head upright.")
    if slots.get('hair') and slots.get('accessory') is None and ORNAMENT_RE.search(slots['hair']):
        problems.append("WARN hair: describes ornaments (pin/ribbon/flower...) which clash with the locked accessory sentence. "
                        "Move the ornaments to --accessory.")
    if slots.get('hair') and slots.get('accessory') and ORNAMENT_RE.search(slots['hair']):
        problems.append("WARN hair: also mentions ornaments while --accessory is set; the two descriptions may fight. Keep ornaments in accessory only.")
    if slots.get('accessory') and RIBBON_RE.search(slots['accessory']):
        problems.append("INFO accessory: thin ribbons floating against the magenta key often leave fringe after postprocess. "
                        "The prompt asks for ribbons tied into the hair and wide enough to read as solid shapes; check the edge numbers.")
    for part in ref_for:
        if not slots.get(part):
            problems.append(f"WARN: ref_for={part} but {part} is not being changed; the reference would fight the KEEP sentence.")
    return problems


def compose_isolate(item, profile, pf):
    """EXPERIMENTAL: ask the edit model to keep ONE layer of an approved portrait and fill the rest with the key colour."""
    slot = item['isolate']
    if slot not in ISOLATE:
        raise SystemExit(f"unknown isolate slot '{slot}'. available: {', '.join(ISOLATE)}")
    clash = [p for p in PARTS if item.get(p)] + (['parts'] if item.get('parts') else [])
    if clash:
        raise SystemExit(f"--isolate cannot be combined with {', '.join(clash)}: run the change round first, "
                         "then isolate from its approved image")
    spec = ISOLATE[slot]
    part = spec['part'].format(poss=pf['poss'])
    lead = spec.get('lead', 'Keep {part} in image 1 exactly as shown:').replace('{part}', part)
    lines = [f"Edit image 1. {lead} " + spec.get('keep', 'the same shape, the same position, the same size') + " and the same ink-brush painting medium.",
             'Fill every other area of the image with the same flat solid magenta #FF00FF background.']
    if spec.get('complete'):
        lines.append(spec['complete'].format(poss=pf['poss']))
    if spec.get('focus'):
        lines.append(spec['focus'].format(poss=pf['poss']))
    if spec.get('hair'):
        lines.append(spec['hair'].format(poss=pf['poss']))
    if spec.get('exclude'):
        lines.append(spec['exclude'].format(poss=pf['poss']))
    lines.append('Only one character. No text, no logo, no watermark.')
    prompt = '\n'.join(lines)
    problems = ['INFO isolate: experimental. Check that the part keeps the same position and size as image 1 with '
                'photocraft_layers.py --reference <approved portrait>; occluded areas are invented by the model.']
    m = NEGATION_RE.search(prompt.replace('No text, no logo, no watermark.', ''))
    if m:
        problems.append(f"WARN isolate: negation word '{m.group(0)}'")
    return prompt, problems, profile


def compose(item, banned=None):
    profile = item.get('profile') or (infer_profile(item.get('parts')) if item.get('parts') else 'female')
    pf = PROFILES[profile]
    if item.get('isolate'):
        return compose_isolate(item, profile, pf)
    problems = []
    from_parts = {}
    if item.get('parts'):
        from_parts, warns = parts_to_slots(item['parts'])
        problems += warns
    slots = {}
    for p in PARTS:
        explicit = item.get(p)
        slots[p] = resolve(profile, p, explicit) if explicit is not None else from_parts.get(p)
    changed = [p for p in PARTS if slots[p]]
    if not changed:
        raise SystemExit('nothing to change: give at least one of ' + '/'.join('--' + p for p in PARTS) + ' or --parts')
    ref_for = parse_ref_for(item.get('ref_for'))
    banned = DEFAULT_BANNED if banned is None else banned
    problems += lint(slots, banned, ref_for)

    keep_phrase = {
        'pose': 'the same standing pose and hand position',
        'face': 'the same face',
        'hair': 'the same hairstyle',
        'accessory': pf['keep_accessory'],
        'outfit': 'the same outfit',
    }
    keep = [pf['body_keep']] + [keep_phrase[p] for p in PARTS if p not in changed]
    keep += ['the same mid-thigh crop', 'the same ink-brush painting medium',
             'and the same flat solid magenta #FF00FF background everywhere outside the figure']
    lines = [f"Edit image 1. Keep {pf['subject']} in image 1 exactly as {pf['sub']} is: " + ', '.join(keep) + '.']
    names = ', '.join(changed[:-1]) + (' and ' if len(changed) > 1 else '') + changed[-1]
    lines.append(f"Change ONLY {pf['poss']} " + names + '.')
    if slots['pose']:
        lines.append(f"{pf['Poss']} pose is now {slots['pose']}. {pf['Poss']} head stays upright at the same position and size in the frame, "
                     "and the figure stays centred with the same mid-thigh crop.")
    if slots['hair']:
        lines.append(f"{pf['Poss']} hair is now {slots['hair']}. The hair is painted in the same {pf['hair_medium']} and dry-brush style as image 1, "
                     "with a clean outline against the flat magenta background.")
    if slots['face']:
        lines.append(f"{pf['Sub']} now has {slots['face']}.")
    if slots['accessory']:
        text = (f"{pf['Sub']} now wears {slots['accessory']}. The ornaments are painted in the same ink-wash style as image 1 "
                "with crisp clean edges.")
        if RIBBON_RE.search(slots['accessory']):
            text += " Any ribbon is tied firmly into the hair and wide enough to read as a solid flowing shape against the flat magenta background."
        lines.append(text)
    if slots['outfit']:
        text = f"{pf['Poss']} clothing is now {slots['outfit']}."
        if SHEER_RE.search(slots['outfit']):
            text += (" Thin fabric is painted with layered pale ink washes and fine dry-brush strokes in the same ink-wash style as image 1. "
                     "Wherever skin is visible through the cloth or a slit it is painted in natural warm skin tone, "
                     "so that the magenta background never shows through the cloth or the skin.")
        lines.append(text)
    if ref_for:
        roles = [REF_ROLE[p] for p in ref_for]
        role = roles[0] if len(roles) == 1 else ', '.join(roles[:-1]) + ' and ' + roles[-1]
        lines.append(f"Image 3 supplies only {role}; every other part of {pf['obj']} and the background stay as in image 1.")
    lines.append('Only one character. No text, no logo, no watermark.')
    prompt = '\n'.join(lines)
    if len(prompt) > 2000:
        problems.append(f'WARN: prompt is {len(prompt)} chars; consider shortening the slot text.')
    return prompt, problems, profile


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    for p in PARTS:
        ap.add_argument('--' + p)
    ap.add_argument('--isolate', help='EXPERIMENTAL: hair_back | body | head | hair_front | accessory - keep only that layer on the key colour (use on an approved portrait)')
    ap.add_argument('--profile', help='female | male | elder_male (default: female, or inferred from --parts)')
    ap.add_argument('--parts', help='JSON {game_slot: part_id} or @file.json, e.g. the disciple\'s AvatarAppearance.Parts')
    ap.add_argument('--ref-for', help='part(s) that image 3 (reference) supplies, comma separated, e.g. outfit,accessory')
    ap.add_argument('--recipe', help='JSON: {"banned_words": [...], "items": [{"id","profile","seed","base","ref","parts","isolate","pose","face","hair","accessory","outfit","ref_for"}]}')
    ap.add_argument('--out', help='write the composed items to this JSON file')
    ap.add_argument('--list', action='store_true', help='list presets')
    args = ap.parse_args()

    if args.list:
        for group, presets in PRESETS.items():
            print(f'### profile presets: {group}')
            for part in PARTS:
                for name, text in presets.get(part, {}).items():
                    print(f'  [{part}] @{name}: {text[:80]}{"..." if len(text) > 80 else ""}')
        return

    if args.recipe:
        recipe = json.loads(Path(args.recipe).read_text(encoding='utf-8'))
        items, banned = recipe['items'], recipe.get('banned_words')
    else:
        parts = None
        if args.parts:
            parts = json.loads(Path(args.parts[1:]).read_text(encoding='utf-8') if args.parts.startswith('@') else args.parts)
        items = [{'id': 'single', 'profile': args.profile, 'parts': parts, 'isolate': args.isolate,
                  **{p: getattr(args, p) for p in PARTS}, 'ref_for': args.ref_for}]
        banned = None

    results, failed = [], False
    for item in items:
        prompt, problems, profile = compose(item, banned)
        print('=' * 78)
        print(f"{item.get('id')}  profile={profile}  seed={item.get('seed')}  base={item.get('base')}  ref={item.get('ref')}")
        print(prompt)
        for p in problems:
            print('  ' + p, file=sys.stderr)
            failed |= p.startswith('ERROR')
        results.append({**item, 'profile': profile, 'prompt': prompt, 'lint': problems})
    if args.out:
        Path(args.out).write_text(json.dumps(results, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        print(f'wrote {args.out}')
    sys.exit(1 if failed else 0)


if __name__ == '__main__':
    main()