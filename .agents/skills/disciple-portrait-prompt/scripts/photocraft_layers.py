#!/usr/bin/env python3
"""Turn isolated, magenta-keyed part images into aligned RGBA layer PNGs with photocraft-cli.

  python photocraft_layers.py --manifest layers.json --work work --out out [--reference base.png]

manifest (parts listed bottom -> top, same order the game composes them):
  {"disciple": "d000", "canvas": [1024, 1536],
   "key": {"color": "#ff00ff", "fuzziness": 40},
   "matting": {"defringe": 3, "decontaminate": {"amount": 100, "radius": 2}},
   "parts": [{"id": "hair_back_topknot_long", "slot": "hair_back", "file": "hair_back.png"},
             {"id": "body_robe_azure", "slot": "body", "file": "body.png"}, ...]}
  "file" is relative to the manifest. Part ids are lowercase [a-z0-9_] and become layer names and file names.

Per part (measured on photocraft-cli 0.7.0: raw colour-range leaves ~3500 key-tinted edge pixels on a test
shape; defringe 3 + colorDecontaminate 100/r2 leaves 1):
  open -> Layer From Background -> Color Range (key) -> Clear -> Deselect -> Defringe -> Colour Decontaminate -> rename
then all parts are copied into one document (positions kept), saved as <out>/<disciple>.pcraft for hand edits,
and exported one PNG per layer at full canvas size.

Why two processes: the MCP/serve automation session refuses file.export.layersToFiles (ambient filesystem path).
This script keeps editing inside a scoped `photocraft-cli serve` session (read/write root = --work) and does the
export with a one-shot `photocraft-cli run`, which the CLI allows.

Checks (numbers only, no taste judgement): size == canvas, mode RGBA, top corners transparent, key-colour
pixels left (near_key_px), alpha bbox. With --reference the layers are re-composited and compared with the
original painting (mismatch fraction) - the real test that the cut did not move or lose pixels.
Exit 1 on any FAIL.
"""
import argparse, json, re, shutil, subprocess, sys
from pathlib import Path

CANONICAL = ['base', 'hair_back', 'body', 'head', 'eyes', 'brows', 'nose', 'mouth', 'eyeshadow',
             'face_marking', 'hair_front', 'accessory']
COARSE = ['hair_back', 'body', 'head', 'hair_front', 'accessory']   # relative order that must hold
ID_RE = re.compile(r'^[a-z0-9_]+$')
TESTED_VERSION = '0.7.0'


class Session:
    """photocraft-cli serve, JSON lines on stdio, filesystem limited to one root."""
    def __init__(self, exe, root, log):
        self.p = subprocess.Popen([exe, 'serve', '--automation-read-root', str(root), '--automation-write-root', str(root)],
                                  stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=log, text=True, encoding='utf-8')
        self.n = 0

    def call(self, method, params=None):
        self.n += 1
        self.p.stdin.write(json.dumps({'id': self.n, 'method': method, 'params': params or {}}) + '\n')
        self.p.stdin.flush()
        line = self.p.stdout.readline()
        if not line:
            raise RuntimeError('photocraft-cli serve closed unexpectedly')
        reply = json.loads(line)
        if not reply.get('ok'):
            raise RuntimeError(f'{method} {params}: {reply.get("error")}')
        return reply['result']

    def run(self, command, params=None):
        return self.call('engine.execute', {'command': command, 'params': params or {}})

    def close(self):
        try:
            self.p.stdin.close()
            self.p.wait(timeout=15)
        except Exception:
            self.p.kill()


def load_manifest(path):
    m = json.loads(Path(path).read_text(encoding='utf-8'))
    base = Path(path).resolve().parent
    m.setdefault('canvas', [1024, 1536])
    m.setdefault('key', {})
    m['key'] = {'color': '#ff00ff', 'fuzziness': 40, **m['key']}
    m.setdefault('matting', {})
    m['matting'] = {'defringe': 3, 'decontaminate': {'amount': 100, 'radius': 2}, **m['matting']}
    if not m.get('disciple') or not ID_RE.match(m['disciple']):
        raise SystemExit('manifest.disciple must be lowercase [a-z0-9_]')
    parts, seen = m.get('parts') or [], set()
    if not parts:
        raise SystemExit('manifest has no parts')
    for part in parts:
        if not ID_RE.match(part.get('id', '')):
            raise SystemExit(f"part id '{part.get('id')}' must be lowercase [a-z0-9_]")
        if part['id'] in seen:
            raise SystemExit(f"duplicate part id '{part['id']}'")
        seen.add(part['id'])
        src = (base / part['file']).resolve()
        if not src.is_file():
            raise SystemExit(f"missing file for {part['id']}: {src}")
        part['_src'] = src
        if part.get('slot') and part['slot'] not in CANONICAL:
            print(f"WARN {part['id']}: slot '{part['slot']}' is not one of {', '.join(CANONICAL)}")
    ranks = [COARSE.index(p['slot']) for p in parts if p.get('slot') in COARSE]
    if ranks != sorted(ranks):
        raise SystemExit('parts are not ordered bottom -> top (expected hair_back, body, head, ..., hair_front, accessory)')
    return m


def find_cli(explicit):
    exe = explicit or shutil.which('photocraft-cli')
    if not exe:
        raise SystemExit('photocraft-cli not found; put it on PATH or pass --photocraft-cli')
    ver = subprocess.run([exe, '--version'], capture_output=True, text=True, encoding='utf-8').stdout.strip()
    print(f'using {exe}: {ver}')
    if TESTED_VERSION not in ver:
        print(f'WARN this script was exercised on photocraft-cli {TESTED_VERSION} (Linux x86_64); other versions may differ')
    return exe


def build(manifest, work, exe):
    inputs = work / 'in'
    inputs.mkdir(parents=True, exist_ok=True)
    for i, part in enumerate(manifest['parts']):
        part['_rel'] = f'in/p{i:02d}.png'
        shutil.copy2(part['_src'], work / part['_rel'])
    key = {'select': 'sampledColors', 'color': manifest['key']['color'], 'fuzziness': manifest['key']['fuzziness']}
    mat = manifest['matting']
    with open(work / 'serve.log', 'w', encoding='utf-8') as log:
        s = Session(exe, work, log)
        try:
            index = {}
            for part in manifest['parts']:
                info = s.call('doc.open', {'path': part['_rel']})
                index[part['id']] = info['index']
                if [info['width'], info['height']] != manifest['canvas']:
                    raise SystemExit(f"FAIL {part['id']}: input is {info['width']}x{info['height']}, canvas is {manifest['canvas']}")
                s.run('document.activate', {'document': info['index']})
                layer = s.run('layer.new.layerFromBackground')['layer']
                picked = s.run('select.colorRange', key)
                if not (picked or {}).get('selected'):
                    raise SystemExit(f"FAIL {part['id']}: no pixel matched the key colour {key['color']}; "
                                     "is the background really flat magenta?")
                s.run('edit.clear')
                s.run('select.deselect')
                if mat.get('defringe'):
                    s.run('layer.matting.defringe', {'width': mat['defringe']})
                if mat.get('decontaminate'):
                    s.run('layer.matting.colorDecontaminate', mat['decontaminate'])
                s.run('layer.renameLayer', {'layer': layer, 'name': part['id']})
                print(f"keyed {part['id']} (doc {info['index']}, layer {layer})")
            first = manifest['parts'][0]['id']
            master = index[first]
            for part in manifest['parts'][1:]:
                s.run('document.activate', {'document': index[part['id']]})
                s.run('layer.copyToDocument', {'document': master, 'source': index[part['id']]})
            s.run('document.activate', {'document': master})
            names = [layer['name'] for layer in s.call('doc.inspect')['layers']]
            expected = [p['id'] for p in reversed(manifest['parts'])]
            if names != expected:
                raise SystemExit(f'FAIL layer order {names} != expected {expected}')
            s.call('doc.save', {'path': 'master.pcraft'})
        finally:
            s.close()
    shutil.rmtree(work / 'export', ignore_errors=True)
    proc = subprocess.run([exe, 'run', 'master.pcraft', '--cmd', 'file.export.layersToFiles', '--params',
                           json.dumps({'dir': 'export', 'format': 'png', 'prefix': 'L'})],
                          cwd=work, capture_output=True, text=True, encoding='utf-8')
    if proc.returncode:
        raise SystemExit(f'export failed: {proc.stdout}\n{proc.stderr}')
    files = {}
    for f in (work / 'export').glob('L_*.png'):
        m = re.match(r'^L_\d+_(.+)\.png$', f.name)
        if m:
            files[m.group(1)] = f
    missing = [p['id'] for p in manifest['parts'] if p['id'] not in files]
    if missing:
        raise SystemExit(f'FAIL export is missing layers: {missing}; got {sorted(files)}')
    return files


def verify(manifest, files, tol=60):
    import numpy as np
    from PIL import Image
    kr, kg, kb = (int(manifest['key']['color'][i:i + 2], 16) for i in (1, 3, 5))
    report, fails = {}, []
    for part in manifest['parts']:
        with Image.open(files[part['id']]) as im:
            size, mode = list(im.size), im.mode
            row = {'size': size, 'mode': mode}
            if size != manifest['canvas']:
                fails.append(f"{part['id']}: size {size} != {manifest['canvas']}")
            if mode != 'RGBA':
                fails.append(f"{part['id']}: mode {mode} != RGBA")
                report[part['id']] = row
                continue
            a = np.asarray(im).astype(int)
        alpha = a[..., 3]
        vis = alpha > 0
        near_key = vis & (abs(a[..., 0] - kr) <= tol) & (abs(a[..., 1] - kg) <= tol) & (abs(a[..., 2] - kb) <= tol)
        ys, xs = vis.nonzero()
        row.update({'top_corner_alpha': [int(alpha[0, 0]), int(alpha[0, -1])], 'near_key_px': int(near_key.sum()),
                    'opaque_px': int((alpha == 255).sum()), 'semi_px': int(((alpha > 0) & (alpha < 255)).sum()),
                    'bbox': [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())] if len(xs) else None})
        if not len(xs):
            fails.append(f"{part['id']}: layer is empty")
        if max(row['top_corner_alpha']) > 0:
            fails.append(f"{part['id']}: top corners are not transparent {row['top_corner_alpha']}")
        if row['near_key_px'] > 50:
            fails.append(f"{part['id']}: {row['near_key_px']} visible pixels still close to the key colour")
        report[part['id']] = row
    return report, fails


def reconstruct(manifest, files, reference):
    """Composite the layers bottom->top over the key colour and compare with the original painting."""
    import numpy as np
    from PIL import Image
    key = tuple(int(manifest['key']['color'][i:i + 2], 16) for i in (1, 3, 5))
    canvas = Image.new('RGBA', tuple(manifest['canvas']), key + (255,))
    for part in manifest['parts']:
        with Image.open(files[part['id']]) as im:
            canvas = Image.alpha_composite(canvas, im.convert('RGBA'))
    with Image.open(reference) as ref:
        ref_rgb = np.asarray(ref.convert('RGB')).astype(int)
    if ref_rgb.shape[:2] != (manifest['canvas'][1], manifest['canvas'][0]):
        return {'error': f'reference is {ref_rgb.shape[1]}x{ref_rgb.shape[0]}, canvas is {manifest["canvas"]}'}
    got = np.asarray(canvas.convert('RGB')).astype(int)
    keyarr = np.array(key)
    in_ref = (abs(ref_rgb - keyarr).max(axis=2) > 60)
    in_got = (abs(got - keyarr).max(axis=2) > 60)
    union = in_ref | in_got
    diff = abs(ref_rgb - got).max(axis=2)
    bad = union & (diff > 48)
    return {'union_px': int(union.sum()), 'mismatch_px': int(bad.sum()),
            'mismatch_fraction': round(float(bad.sum()) / max(1, int(union.sum())), 5),
            'only_in_reference_px': int((in_ref & ~in_got).sum()), 'only_in_layers_px': int((in_got & ~in_ref).sum())}


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--manifest', required=True)
    ap.add_argument('--work', required=True, help='scratch folder (also the scoped read/write root of the session)')
    ap.add_argument('--out', required=True, help='final folder: <id>.png per part, <disciple>.pcraft, layers_report.json')
    ap.add_argument('--photocraft-cli', help='path to photocraft-cli (default: PATH)')
    ap.add_argument('--reference', help='the original painting (magenta background) to re-composite against')
    ap.add_argument('--max-mismatch', type=float, help='FAIL when mismatch_fraction exceeds this (needs --reference)')
    a = ap.parse_args()

    manifest = load_manifest(a.manifest)
    work, out = Path(a.work).resolve(), Path(a.out).resolve()
    work.mkdir(parents=True, exist_ok=True)
    out.mkdir(parents=True, exist_ok=True)
    exe = find_cli(a.photocraft_cli)
    try:
        files = build(manifest, work, exe)
    except RuntimeError as e:
        raise SystemExit(f'FAIL photocraft-cli: {e}')
    report, fails = verify(manifest, files)
    result = {'disciple': manifest['disciple'], 'canvas': manifest['canvas'], 'parts': report}
    if a.reference:
        result['reconstruction'] = reconstruct(manifest, files, a.reference)
        rec = result['reconstruction']
        if 'error' in rec:
            fails.append('reference: ' + rec['error'])
        elif a.max_mismatch is not None and rec['mismatch_fraction'] > a.max_mismatch:
            fails.append(f"reconstruction mismatch {rec['mismatch_fraction']} > {a.max_mismatch}")
    for part in manifest['parts']:
        shutil.copy2(files[part['id']], out / f"{part['id']}.png")
    shutil.copy2(work / 'master.pcraft', out / f"{manifest['disciple']}.pcraft")
    result['fails'] = fails
    (out / 'layers_report.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result, indent=2))
    print(f"wrote {len(manifest['parts'])} layers + {manifest['disciple']}.pcraft to {out}")
    if fails:
        print('FAIL:\n  ' + '\n  '.join(fails))
        sys.exit(1)


if __name__ == '__main__':
    main()