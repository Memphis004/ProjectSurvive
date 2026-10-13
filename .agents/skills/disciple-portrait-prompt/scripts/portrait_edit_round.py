#!/usr/bin/env python3
"""Generic ComfyUI round runner for disciple portrait jobs.

Replaces the missing portrait_edit_round.py referenced by SKILL.md. Built from the
layers_trial temp runner (run_head_trial.py) and the run() pattern of
Tools/art/portrait_female_candidates.py.

Input : prompts.json from compose_portrait_prompt.py (items with prompt/seed/base/ref)
Graph : --template baseline workflow (node ids 4 base, 5 style anchor, 6 prompt,
        8 seed, 10 filename_prefix, 11 reference)

Node 11 (image_3): by default the link to the encoder is REMOVED (and node 11 dropped)
unless an item sets "ref" or --keep-image3 is passed. Reason: the composed prompt does
not describe image 3, so a connected reference is an unexplained contamination source.

Modes
  --preview      build graphs + validate; submits nothing (default)
  --prepare-only build + validate + write workflows; submits nothing
  --run          submit jobs one at a time (needs --approved-images >= number of jobs)

Accounting: out_dir/generation_log.json is written BEFORE and AFTER every submit.
A job left in 'submitting' or 'submitted' is never resubmitted automatically.
Sentinel: out_dir/pause_requested is checked before every submit.

Usage
  python portrait_edit_round.py --items prompts.json --round-name r10 --group male \
      --template art/portraits_v2/workflows/d003_seed430301.json --approved-images 3 --preview
"""
import argparse
import copy
import json
import pathlib
import re
import shutil
import subprocess
import sys
import urllib.request

HERE = pathlib.Path(__file__).resolve()
ROOT = HERE.parents[4]  # .agents/skills/<skill>/scripts -> repo root
COMFY = "http://127.0.0.1:8188"
NODE_BASE, NODE_ANCHOR, NODE_PROMPT, NODE_SEED, NODE_SAVE, NODE_REF = "4", "5", "6", "8", "10", "11"
BANNED_DEFAULT = ["braid", "plait", "cornrow"]
FALLBACK_INPUT = pathlib.Path(r"C:\Users\memph\AppData\Local\Comfy-Desktop\ComfyUI-Shared\input")
FALLBACK_OUTPUT = pathlib.Path(r"C:\Users\memph\AppData\Local\Comfy-Desktop\ComfyUI-Shared\output")


def server_dirs():
    try:
        with urllib.request.urlopen(f"{COMFY}/system_stats", timeout=10) as r:
            argv = json.load(r)["system"]["argv"]
    except Exception as exc:  # noqa: BLE001
        print(f"! system_stats failed ({exc}); using fallback dirs", file=sys.stderr)
        return FALLBACK_INPUT, FALLBACK_OUTPUT
    inp = out = None
    for i, tok in enumerate(argv):
        if tok == "--input-directory" and i + 1 < len(argv):
            inp = pathlib.Path(argv[i + 1])
        elif tok == "--output-directory" and i + 1 < len(argv):
            out = pathlib.Path(argv[i + 1])
    return inp or FALLBACK_INPUT, out or FALLBACK_OUTPUT


def cli(*args):
    proc = subprocess.run(["comfy", *args], capture_output=True, text=True, encoding="utf-8", errors="replace")
    if proc.returncode:
        raise RuntimeError(f"comfy {args} exit {proc.returncode}: {proc.stdout}\n{proc.stderr}")
    payload = json.loads(proc.stdout)
    if payload.get("type") == "envelope":
        if not payload.get("ok"):
            raise RuntimeError(payload)
        return payload["data"]
    return payload


def save_log(path, log):
    tmp = path.with_suffix(".tmp")
    tmp.write_text(json.dumps(log, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    tmp.replace(path)


def check_prompt(prompt, banned):
    hits = [w for w in banned if re.search(w, prompt, re.I)]
    if hits:
        raise SystemExit(f"ERROR prompt contains banned word(s) {hits}; fix the brief before generating.")


def build_graph(template, item, base_name, ref_name, anchor_name, keep_image3, banned, prefix):
    g = copy.deepcopy(template)
    check_prompt(item["prompt"], banned)
    g[NODE_BASE]["inputs"]["image"] = base_name
    if anchor_name:
        g[NODE_ANCHOR]["inputs"]["image"] = anchor_name
    g[NODE_PROMPT]["inputs"]["prompt"] = item["prompt"]
    g[NODE_SEED]["inputs"]["seed"] = int(item["seed"])
    g[NODE_SAVE]["inputs"]["filename_prefix"] = f"{prefix}_seed{item['seed']}"
    enc = g[NODE_PROMPT]["inputs"]
    if ref_name:
        g[NODE_REF]["inputs"]["image"] = ref_name
    elif not keep_image3:
        enc.pop("images.image_3", None)
        g.pop(NODE_REF, None)
    return g


def stage(src, inp_dir, name):
    inp_dir.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, inp_dir / name)
    return name


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--items", required=True, help="prompts.json from compose_portrait_prompt.py")
    ap.add_argument("--round-name", required=True)
    ap.add_argument("--group", required=True, help="e.g. female | male | elder | layers")
    ap.add_argument("--template", required=True, help="baseline workflow JSON")
    ap.add_argument("--anchor", help="optional style-anchor PNG for node 5 (else template value is kept)")
    ap.add_argument("--assets-dir", default=None, help="folder holding base/ref files named in items")
    ap.add_argument("--approved-images", type=int, default=0, help="quota: max submissions this round")
    ap.add_argument("--keep-image3", action="store_true", help="keep node 11 wired when no item has ref")
    ap.add_argument("--out-dir", default=None, help="default art/portraits_v2/<group>_round_<name>")
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--preview", action="store_true")
    mode.add_argument("--prepare-only", action="store_true")
    mode.add_argument("--run", action="store_true")
    args = ap.parse_args()

    items = json.loads(pathlib.Path(args.items).read_text(encoding="utf-8"))
    template = json.loads(pathlib.Path(args.template).read_text(encoding="utf-8"))
    out_dir = pathlib.Path(args.out_dir) if args.out_dir else ROOT / "art" / "portraits_v2" / f"{args.group}_round_{args.round_name}"
    out_dir.mkdir(parents=True, exist_ok=True)
    wdir = out_dir / "workflows"
    wdir.mkdir(exist_ok=True)
    inp_dir, out_comfy = server_dirs()
    print(f"comfy input={inp_dir} output={out_comfy}")

    banned = BANNED_DEFAULT
    prefix = f"portrait_{args.group}_{args.round_name}"
    anchor_name = None
    if args.anchor:
        anchor_name = stage(ROOT / args.anchor if not pathlib.Path(args.anchor).is_absolute() else args.anchor,
                            inp_dir, f"{prefix}_anchor.png")
    graphs = []
    for item in items:
        base_name = ref_name = None
        if item.get("base"):
            src = pathlib.Path(args.assets_dir or ".") / item["base"]
            base_name = stage(src, inp_dir, f"{prefix}_{item['seed']}_base.png")
        if item.get("ref"):
            src = pathlib.Path(args.assets_dir or ".") / item["ref"]
            ref_name = stage(src, inp_dir, f"{prefix}_{item['seed']}_ref.png")
        g = build_graph(template, item, base_name or template[NODE_BASE]["inputs"]["image"], ref_name,
                        anchor_name, args.keep_image3, banned, prefix)
        graphs.append((item, g))

    if args.preview or args.prepare_only:
        mode_label = "preview" if args.preview else "prepare-only"
    else:
        mode_label = "run"
        if args.approved_images < len(graphs):
            raise SystemExit(f"quota: {len(graphs)} jobs but --approved-images={args.approved_images}; refusing.")

    report = []
    for item, g in graphs:
        path = wdir / f"seed{item['seed']}.json"
        path.write_text(json.dumps(g, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        v = cli("workflow", "validate", "--workflow", str(path))
        clean = v.get("valid") and not v.get("error_count") and not v.get("warning_count") and not v.get("spends_credits")
        report.append({"seed": item["seed"], "workflow": str(path), "validate": v, "cleared": bool(clean),
                       "image3_linked": NODE_REF in g})
        print(f"seed {item['seed']}: valid={v.get('valid')} errors={v.get('error_count')} "
              f"warnings={v.get('warning_count')} spends={v.get('spends_credits')} image3_linked={NODE_REF in g}")
    if mode_label != "run":
        save_log(out_dir / "preview_report.json", report)
        print(f"{mode_label}: nothing submitted. report -> {out_dir / 'preview_report.json'}")
        return 0 if all(r["cleared"] for r in report) else 1

    log_path = out_dir / "generation_log.json"
    log = json.loads(log_path.read_text(encoding="utf-8")) if log_path.exists() else {
        "round": args.round_name, "group": args.group, "approved_images": args.approved_images,
        "generation_requests_submitted": 0, "jobs": []}
    if any(j.get("status") in ("submitting", "submitted") for j in log["jobs"]):
        raise SystemExit("ambiguous previous submit recorded in log; reconcile the queue before running.")
    sentinel = out_dir / "pause_requested"
    for item, g in graphs:
        if sentinel.exists():
            print("PAUSED: pause_requested present; no further submits.")
            break
        if any(j["seed"] == item["seed"] and j["status"] == "completed" for j in log["jobs"]):
            continue
        if log["generation_requests_submitted"] >= args.approved_images:
            raise SystemExit("quota reached; stop and ask the user before any more submits.")
        if not next(r for r in report if r["seed"] == item["seed"])["cleared"]:
            raise SystemExit(f"seed {item['seed']} did not validate cleanly; not submitted.")
        path = wdir / f"seed{item['seed']}.json"
        job = {"seed": item["seed"], "workflow": str(path), "status": "submitting"}
        log["jobs"].append(job)
        save_log(log_path, log)  # intent before submit
        sub = cli("run", "--workflow", str(path), "--where", "local", "--host", "127.0.0.1", "--port", "8188",
                  "--no-notify", "--no-watch")
        job["prompt_id"] = sub["prompt_id"]
        job["status"] = "submitted"
        log["generation_requests_submitted"] += 1
        save_log(log_path, log)
        cli("jobs", "wait", job["prompt_id"], "--host", "127.0.0.1", "--port", "8188", "--timeout", "900",
            "--poll-interval", "10")
        st = cli("jobs", "status", job["prompt_id"], "--host", "127.0.0.1", "--port", "8188")
        if st.get("status") != "completed" or st.get("execution_error") or st.get("error"):
            job["status"] = "failed_or_ambiguous"
            save_log(log_path, log)
            raise SystemExit(f"job failed/ambiguous (do not resubmit): {st}")
        dl = cli("download", job["prompt_id"], "--where", "local", "-o", str(out_dir / "raw" / str(item["seed"])))
        files = dl.get("files", [])
        if len(files) != 1:
            job["status"] = "download_ambiguous"
            save_log(log_path, log)
            raise SystemExit(f"expected exactly 1 output: {dl}")
        job["status"] = "completed"
        job["raw"] = files[0]["path"]
        save_log(log_path, log)
        print("completed", item["seed"], files[0]["path"])
    return 0


if __name__ == "__main__":
    sys.exit(main())
